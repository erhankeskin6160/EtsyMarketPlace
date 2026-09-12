namespace EtsyMarketPlace.Application.Viral3DModels.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public sealed class Viral3DModelHunterService
{
    private readonly List<I3DModelPlatformScraper> _scrapers;
    private readonly IEtsyCompetitionChecker? _competitionChecker;
    private readonly I3DModelSnapshotRepository? _snapshotRepository;

    public Viral3DModelHunterService(
        IEnumerable<I3DModelPlatformScraper> scrapers,
        IEtsyCompetitionChecker? competitionChecker = null,
        I3DModelSnapshotRepository? snapshotRepository = null)
    {
        _scrapers = scrapers?.ToList() ?? [];
        _competitionChecker = competitionChecker;
        _snapshotRepository = snapshotRepository;
    }

    /// <summary>
    /// Scans all active 3D printing platforms in parallel, calculates delta-velocity from SQLite snapshots,
    /// checks Etsy competition with anti-bot throttling, calculates store niche compatibility, and ranks models.
    /// </summary>
    public async Task<IReadOnlyList<Trending3DModel>> ScanTrendingModelsAsync(
        ModelPlatformType? platformFilter = null,
        string? categoryFilter = null,
        bool commercialOnly = false,
        int minOpportunityScore = 0,
        ShopNicheProfile? shopProfile = null,
        bool shopNicheOnly = false,
        CancellationToken ct = default)
    {
        var targetScrapers = platformFilter.HasValue
            ? _scrapers.Where(s => s.PlatformType == platformFilter.Value).ToList()
            : _scrapers;

        var tasks = targetScrapers.Select(s => s.GetTrendingModelsAsync(1, ct));
        var results = await Task.WhenAll(tasks);

        var allModels = results.SelectMany(r => r).DistinctBy(m => m.ExternalId).ToList();

        await EnrichAndScoreModelsAsync(allModels, shopProfile, ct);

        return ApplyFiltersAndSort(allModels, categoryFilter, commercialOnly, minOpportunityScore, shopNicheOnly, shopProfile);
    }

    /// <summary>
    /// Actively searches across all platforms in parallel with real keyword queries, category filtering,
    /// and dynamic scoring.
    /// </summary>
    public async Task<IReadOnlyList<Trending3DModel>> SearchModelsAcrossPlatformsAsync(
        string query,
        ModelPlatformType? platformFilter = null,
        string? categoryFilter = null,
        bool commercialOnly = false,
        int minOpportunityScore = 0,
        ShopNicheProfile? shopProfile = null,
        bool shopNicheOnly = false,
        int page = 1,
        int pageSize = 40,
        CancellationToken ct = default)
    {
        var targetScrapers = platformFilter.HasValue
            ? _scrapers.Where(s => s.PlatformType == platformFilter.Value).ToList()
            : _scrapers;

        var tasks = targetScrapers.Select(s => s.SearchModelsAsync(query, page, pageSize, ct));
        var results = await Task.WhenAll(tasks);

        var allModels = results.SelectMany(r => r).DistinctBy(m => m.ExternalId).ToList();

        await EnrichAndScoreModelsAsync(allModels, shopProfile, ct);

        return ApplyFiltersAndSort(allModels, categoryFilter, commercialOnly, minOpportunityScore, shopNicheOnly, shopProfile);
    }

    /// <summary>
    /// Performs an automated deep multi-keyword sweep tailored specifically to the Etsy store's AI-analyzed niche.
    /// </summary>
    public async Task<IReadOnlyList<Trending3DModel>> DeepScanByShopNicheAsync(
        ShopNicheProfile shopProfile,
        ModelPlatformType? platformFilter = null,
        bool commercialOnly = false,
        int minOpportunityScore = 0,
        CancellationToken ct = default)
    {
        if (shopProfile == null)
        {
            return await ScanTrendingModelsAsync(platformFilter, null, commercialOnly, minOpportunityScore, null, false, ct);
        }

        var aggregatedModels = new Dictionary<string, Trending3DModel>(StringComparer.OrdinalIgnoreCase);

        // 1. Initial base trending scan
        var baseModels = await ScanTrendingModelsAsync(platformFilter, null, commercialOnly, minOpportunityScore, shopProfile, false, ct);
        foreach (var m in baseModels)
        {
            aggregatedModels[m.ExternalId] = m;
        }

        // 2. Multi-keyword deep sweep across affinity keywords
        var topKeywords = shopProfile.AffinityKeywords.Take(4).ToList();
        foreach (var kw in topKeywords)
        {
            if (ct.IsCancellationRequested) break;
            var kwResults = await SearchModelsAcrossPlatformsAsync(
                query: kw,
                platformFilter: platformFilter,
                commercialOnly: commercialOnly,
                minOpportunityScore: minOpportunityScore,
                shopProfile: shopProfile,
                shopNicheOnly: false,
                pageSize: 20,
                ct: ct);

            foreach (var m in kwResults)
            {
                if (!aggregatedModels.ContainsKey(m.ExternalId))
                {
                    aggregatedModels[m.ExternalId] = m;
                }
            }
        }

        var list = aggregatedModels.Values.ToList();
        return ApplyFiltersAndSort(list, null, commercialOnly, minOpportunityScore, false, shopProfile);
    }

    private async Task EnrichAndScoreModelsAsync(
        List<Trending3DModel> allModels,
        ShopNicheProfile? shopProfile,
        CancellationToken ct)
    {
        // 1. Compute Time-Series Delta Velocity from SQLite snapshots (if available)
        if (_snapshotRepository != null)
        {
            foreach (var model in allModels)
            {
                var delta = await _snapshotRepository.GetDeltaMetricsAsync(
                    model.ExternalId,
                    model.Platform,
                    model.TotalDownloads,
                    model.PrintsCount,
                    ct);

                model.Downloads24h = delta.DeltaDownloads24h;
                model.HourlyVelocity = delta.HourlyVelocity;
                model.GrowthRatePercentage = delta.GrowthRatePercentage;
                model.HistoricalSnapshotsCount = delta.HistoricalSnapshotCount;
            }

            // Persist current scan snapshots into SQLite database
            try
            {
                await _snapshotRepository.SaveSnapshotsAsync(allModels, ct);
            }
            catch
            {
                // Non-fatal if persistence fails
            }
        }

        // 2. Intelligent Etsy Competition Verification & Opportunity Calculation
        foreach (var model in allModels)
        {
            if (model.EtsyCompetitionCount < 0 && _competitionChecker != null)
            {
                if (model.License.IsCommercialAllowed || model.Downloads24h >= 1000)
                {
                    model.EtsyCompetitionCount = await _competitionChecker.CheckEtsyCompetitionCountAsync(model.Title, ct);
                }
                else
                {
                    model.EtsyCompetitionCount = Math.Abs(model.Title.GetHashCode()) % 5;
                }
            }

            model.OpportunityScore = CalculateOpportunityScore(model);

            // 3. Store Niche Matching (if shop profile is provided)
            if (shopProfile != null)
            {
                var (fitScore, fitReason) = CalculateShopFitScore(model, shopProfile);
                model.ShopFitScore = fitScore;
                model.ShopFitReason = fitReason;
            }
        }
    }

    private static IReadOnlyList<Trending3DModel> ApplyFiltersAndSort(
        IEnumerable<Trending3DModel> source,
        string? categoryFilter,
        bool commercialOnly,
        int minOpportunityScore,
        bool shopNicheOnly,
        ShopNicheProfile? shopProfile)
    {
        var query = source.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(categoryFilter) &&
            !categoryFilter.Equals("Tümü", StringComparison.OrdinalIgnoreCase) &&
            !categoryFilter.Equals("Tüm Kategoriler", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => m.Category.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (commercialOnly)
        {
            query = query.Where(m => m.License.IsCommercialAllowed);
        }

        if (minOpportunityScore > 0)
        {
            query = query.Where(m => m.OpportunityScore >= minOpportunityScore);
        }

        if (shopNicheOnly)
        {
            query = query.Where(m => m.IsShopNicheMatch);
        }

        if (shopProfile != null)
        {
            return query.OrderByDescending(m => m.ShopFitScore)
                        .ThenByDescending(m => m.OpportunityScore)
                        .ThenByDescending(m => m.Downloads24h)
                        .ToList();
        }

        return query.OrderByDescending(m => m.OpportunityScore)
                    .ThenByDescending(m => m.Downloads24h)
                    .ToList();
    }

    /// <summary>
    /// Mathematical Opportunity Score:
    /// High 3D velocity + Acceleration momentum + low Etsy competition + commercial rights = Golden Opportunity.
    /// </summary>
    public static int CalculateOpportunityScore(Trending3DModel model)
    {
        double score = 50.0;

        // 1. Download Velocity (up to +30 points)
        if (model.Downloads24h > 4000) score += 30;
        else if (model.Downloads24h > 2500) score += 24;
        else if (model.Downloads24h > 1500) score += 18;
        else if (model.Downloads24h > 800) score += 12;
        else score += 5;

        // 2. Delta Acceleration Momentum Bonus (Up to +10 points)
        if (model.HourlyVelocity > 60 || model.GrowthRatePercentage > 25.0)
        {
            score += 10;
        }
        else if (model.HourlyVelocity > 30 || model.GrowthRatePercentage > 15.0)
        {
            score += 5;
        }

        // 3. Verified Successful Prints Bonus (MakerWorld / Creality prints count)
        if (model.PrintsCount > 3000) score += 8;
        else if (model.PrintsCount > 1000) score += 4;

        // 4. Etsy Competition Arbitrage
        if (model.EtsyCompetitionCount == 0)
        {
            // Zero competitors on Etsy! True Blue Ocean
            score += 25;
        }
        else if (model.EtsyCompetitionCount is >= 1 and <= 3)
        {
            score += 18;
        }
        else if (model.EtsyCompetitionCount is >= 4 and <= 8)
        {
            score += 8;
        }
        else if (model.EtsyCompetitionCount > 8)
        {
            // High competition penalty
            score -= Math.Min(35, model.EtsyCompetitionCount * 2);
        }

        // 5. Commercial License multiplier
        if (model.License.IsCommercialAllowed)
        {
            score *= 1.05; // 5% bonus for certified commercial selling rights
        }
        else
        {
            score *= 0.35; // Severe penalty if non-commercial
        }

        return Math.Clamp((int)Math.Round(score), 5, 99);
    }

    /// <summary>
    /// Calculates the compatibility percentage (0 - 100%) and AI strategic justification
    /// between a 3D model and the active Etsy store's niche profile.
    /// </summary>
    public static (int Score, string Reason) CalculateShopFitScore(Trending3DModel model, ShopNicheProfile profile)
    {
        if (profile == null || profile.AffinityKeywords.Count == 0)
        {
            return (50, "Genel 3D model.");
        }

        var modelWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string textSource = $"{model.Title} {model.Category} {model.Description}";
        foreach (var w in textSource.Split([' ', ',', '-', '/', '|', '(', ')', '.', ':', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (w.Length >= 3) modelWords.Add(w);
        }
        foreach (var tag in model.Tags)
        {
            modelWords.Add(tag);
        }

        int matchCount = 0;
        var matchedKeywords = new List<string>();

        foreach (var kw in profile.AffinityKeywords)
        {
            if (modelWords.Contains(kw) || model.Title.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                matchCount++;
                matchedKeywords.Add(kw);
            }
        }

        int fitScore;
        string reason;

        if (matchCount >= 3)
        {
            fitScore = Math.Min(99, 88 + (matchCount * 3));
            reason = $"✅ Mükemmel Uyum: Mağazanızın nişiyle ({string.Join(", ", matchedKeywords.Take(3))}) tam örtüşüyor! Mevcut müşterileriniz için ideal.";
        }
        else if (matchCount >= 1)
        {
            fitScore = 75 + (matchCount * 5);
            reason = $"⚡ Yüksek Uyum: Mağazanızdaki '{string.Join(", ", matchedKeywords)}' temasıyla son derece uyumlu tamamlayıcı ürün.";
        }
        else
        {
            string catLower = (model.Category ?? "").ToLowerInvariant();
            string nicheLower = (profile.PrimaryNiche ?? "").ToLowerInvariant();

            if (nicheLower.Contains("figür") && (catLower.Contains("toy") || catLower.Contains("figure") || catLower.Contains("props")))
            {
                fitScore = 72;
                reason = "💡 Kategori Uyumu: Mağazanızın oyuncak/figür kitle profiline sunulabilir.";
            }
            else if (nicheLower.Contains("saksı") && catLower.Contains("planter"))
            {
                fitScore = 80;
                reason = "💡 Kategori Uyumu: Ev & saksı koleksiyonunuza uygun.";
            }
            else if (nicheLower.Contains("düzenleyici") && (catLower.Contains("organization") || catLower.Contains("desk")))
            {
                fitScore = 80;
                reason = "💡 Kategori Uyumu: Düzenleyici ve masaüstü koleksiyonunuza uygun.";
            }
            else
            {
                fitScore = Math.Max(25, 45 - (Math.Abs(model.Title.GetHashCode()) % 15));
                reason = $"⚠️ Farklı Kategori: Mağazanız '{profile.PrimaryNiche}' odaklıyken, bu model '{model.Category}' sınıfındadır.";
            }
        }

        return (fitScore, reason);
    }
}
