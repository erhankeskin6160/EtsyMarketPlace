namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.ProductOpportunity;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

/// <summary>
/// Personalized autonomous/on-demand product research agent scoped strictly to a single user's Etsy shop.
/// Pipeline: Inspect Shop DNA -> Load Decision Memory -> Plan Search Queries -> Tool Calling -> Profit/Risk/Cannibalization Guard -> Persist.
/// </summary>
public sealed class ShopResearchAgentOrchestrator
{
    private readonly IShopResearchRepository _repository;
    private readonly IEtsyListingService _listingService;
    private readonly IMarketResearchService _marketResearchService;
    private readonly ShopDnaBuilderService _dnaBuilder;
    private readonly CannibalizationGuardService _cannibalizationGuard;
    private readonly ProductOpportunityScorer _opportunityScorer;
    private readonly IGeminiListingApiClient? _llmClient;
    private readonly IViral3DModelLakeRepository? _viral3DLake;

    public ShopResearchAgentOrchestrator(
        IShopResearchRepository repository,
        IEtsyListingService listingService,
        IMarketResearchService marketResearchService,
        ShopDnaBuilderService dnaBuilder,
        CannibalizationGuardService cannibalizationGuard,
        ProductOpportunityScorer opportunityScorer,
        IGeminiListingApiClient? llmClient = null,
        IViral3DModelLakeRepository? viral3DLake = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _listingService = listingService ?? throw new ArgumentNullException(nameof(listingService));
        _marketResearchService = marketResearchService ?? throw new ArgumentNullException(nameof(marketResearchService));
        _dnaBuilder = dnaBuilder ?? throw new ArgumentNullException(nameof(dnaBuilder));
        _cannibalizationGuard = cannibalizationGuard ?? throw new ArgumentNullException(nameof(cannibalizationGuard));
        _opportunityScorer = opportunityScorer ?? throw new ArgumentNullException(nameof(opportunityScorer));
        _llmClient = llmClient;
        _viral3DLake = viral3DLake;
    }

    public async Task<ShopDnaProfile> GetOrBuildDnaAsync(
        string ownerUserId,
        string shopId,
        bool forceRefreshFromShop = false,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetDnaProfileAsync(ownerUserId, shopId, ct);
        if (existing != null && !forceRefreshFromShop)
        {
            return existing;
        }

        IReadOnlyList<ShopListingItemDto> listings = Array.Empty<ShopListingItemDto>();
        try
        {
            listings = await _listingService.GetShopListingsAsync(shopId, 100, ct);
        }
        catch
        {
            // Keep existing profile if Etsy API is temporarily unavailable
            if (existing != null) return existing;
        }

        var profile = _dnaBuilder.BuildFromListings(ownerUserId, shopId, listings, existing);
        await _repository.UpsertDnaProfileAsync(profile, ct);
        return profile;
    }

    public async Task<ResearchRun> RunResearchAsync(
        string ownerUserId,
        string shopId,
        ResearchTriggerType triggerType,
        RunShopResearchRequest? request = null,
        CancellationToken ct = default)
    {
        var run = new ResearchRun
        {
            RunId = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            ShopId = shopId,
            TriggerType = triggerType,
            Status = ResearchRunStatus.Running,
            StartedAtUtc = DateTime.UtcNow
        };

        try
        {
            var dna = await GetOrBuildDnaAsync(ownerUserId, shopId, forceRefreshFromShop: false, ct);
            IReadOnlyList<ShopListingItemDto> ownListings = Array.Empty<ShopListingItemDto>();
            try
            {
                ownListings = await _listingService.GetShopListingsAsync(shopId, 100, ct);
            }
            catch
            {
                // Continue even if live listing fetch fails; cannibalization guard handles empty list gracefully.
            }

            var recentDecisions = await _repository.GetRecentDecisionEventsAsync(ownerUserId, shopId, 25, ct);
            var (queries, hypothesesJson) = await PlanQueriesAsync(dna, recentDecisions, request, ct);
            run.SearchQueriesUsed = queries;
            run.HypothesesJson = hypothesesJson;

            var candidates = new List<ResearchCandidate>();
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Tool 1: Etsy Market Research per planned query
            foreach (var q in queries.Take(4))
            {
                ct.ThrowIfCancellationRequested();
                MarketSearchResponse marketResult;
                try
                {
                    marketResult = await _marketResearchService.SearchMarketAsync(
                        new MarketSearchQuery(q, Limit: 25, SortBy: "market_score", ShopId: shopId),
                        ct);
                }
                catch
                {
                    continue;
                }

                foreach (var item in marketResult.Listings.OrderByDescending(l => l.MarketScore).Take(10))
                {
                    if (string.IsNullOrWhiteSpace(item.Title) || !seenTitles.Add(item.Title.Trim()))
                        continue;

                    var candidate = EvaluateMarketCandidate(
                        run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions, q, marketResult.Kpis, item);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            // Tool 2: If the store's DNA supports 3D printing, also scan Viral 3D Model Lake
            if (dna.Supports3DPrint && _viral3DLake != null)
            {
                try
                {
                    var lakeModels = await _viral3DLake.GetModelsAsync(30, 0, ct);
                    foreach (var model in lakeModels.Where(m => m.License.IsCommercialAllowed).Take(10))
                    {
                        if (string.IsNullOrWhiteSpace(model.Title) || !seenTitles.Add(model.Title.Trim()))
                            continue;

                        var candidate = Evaluate3DCandidate(
                            run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions, model);
                        if (candidate != null)
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
                catch
                {
                    // Optional enrichment; do not fail run if lake is empty
                }
            }

            var maxCount = Math.Clamp(request?.MaxCandidates ?? 10, 3, 25);
            var ranked = candidates
                .OrderByDescending(c => c.OpportunityScore)
                .ThenByDescending(c => c.EstimatedNetProfitUsd)
                .Take(maxCount)
                .ToList();

            run.Candidates = ranked;
            run.Status = ResearchRunStatus.Completed;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.Summary = ranked.Count > 0
                ? $"{queries.Count} niş hipotezi tarandı; mağaza DNA'sına uygun, net kâr ve telif/yamyamlaşma filtresinden geçen {ranked.Count} ürün adayı bulundu."
                : "Tarama tamamlandı ancak mevcut kâr marjı, telif ve yamyamlaşma kriterlerini geçen yeni aday bulunamadı.";

            await _repository.SaveRunAsync(run, ct);
            if (triggerType != ResearchTriggerType.Manual)
            {
                await _repository.UpdateLastScheduledRunAsync(ownerUserId, shopId, DateTime.UtcNow, ct);
            }

            return run;
        }
        catch (Exception ex)
        {
            run.Status = ResearchRunStatus.Failed;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.ErrorMessage = ex.Message;
            run.Summary = $"Araştırma çalıştırılırken hata oluştu: {ex.Message}";
            await _repository.SaveRunAsync(run, ct);
            return run;
        }
    }

    private async Task<(IReadOnlyList<string> Queries, string HypothesesJson)> PlanQueriesAsync(
        ShopDnaProfile dna,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        RunShopResearchRequest? request,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request?.FocusHint))
        {
            var hint = request.FocusHint.Trim();
            var fallback = BuildDeterministicQueries(dna, hint);
            return (fallback, JsonSerializer.Serialize(new[] { new { query = hint, reason = "Kullanıcı odak ipucu" } }));
        }

        if (_llmClient != null && !string.IsNullOrWhiteSpace(request?.ApiKey))
        {
            try
            {
                var accepted = recentDecisions.Where(d => d.Decision == CandidateDecisionStatus.Accepted || d.Decision == CandidateDecisionStatus.SentToDraft)
                    .Select(d => d.CandidateTitle).Take(5).ToList();
                var rejected = recentDecisions.Where(d => d.Decision == CandidateDecisionStatus.Rejected)
                    .Select(d => $"{d.CandidateTitle} ({d.Reason})").Take(5).ToList();

                var sysPrompt =
                    "Sen uzman bir Etsy ürün araştırma ajanısın. Verilen Mağaza DNA'sına ve geçmiş kabul/red kararlarına göre " +
                    "mağazanın mevcut ürünleriyle çakışmayan ama aynı kitleye satabilecek 4 adet yüksek potansiyelli İngilizce Etsy arama sorgusu üret. " +
                    "Yanıtı YALNIZCA geçerli JSON dizisi olarak ver: [{\"query\":\"...\",\"reason\":\"...\"}]";

                var userPrompt = JsonSerializer.Serialize(new
                {
                    niche = dna.PrimaryNiche,
                    priceBandUsd = $"{dna.TargetPriceMinUsd}-{dna.TargetPriceMaxUsd}",
                    supports3DPrint = dna.Supports3DPrint,
                    topTags = dna.TopPerformingTags.Take(10),
                    forbidden = dna.ForbiddenKeywords,
                    recentlyAccepted = accepted,
                    recentlyRejected = rejected
                });

                var model = string.IsNullOrWhiteSpace(request.Model) ? "gemini-2.5-flash" : request.Model;
                var (ok, content, _) = await _llmClient.GenerateOptimizationJsonAsync(sysPrompt, userPrompt, model, request.ApiKey, ct);
                if (ok && !string.IsNullOrWhiteSpace(content))
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            if (el.TryGetProperty("query", out var qEl) && !string.IsNullOrWhiteSpace(qEl.GetString()))
                            {
                                list.Add(qEl.GetString()!.Trim());
                            }
                        }
                        if (list.Count > 0)
                        {
                            return (list.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList(), content);
                        }
                    }
                }
            }
            catch
            {
                // Fallback to deterministic query planner
            }
        }

        var deterministic = BuildDeterministicQueries(dna, null);
        var json = JsonSerializer.Serialize(deterministic.Select(q => new { query = q, reason = "Mağaza DNA anahtar kelimesi" }));
        return (deterministic, json);
    }

    private static IReadOnlyList<string> BuildDeterministicQueries(ShopDnaProfile dna, string? focusHint)
    {
        var queries = new List<string>();
        if (!string.IsNullOrWhiteSpace(focusHint))
            queries.Add(focusHint.Trim());

        foreach (var tag in dna.TopPerformingTags.Concat(dna.CategoryKeywords))
        {
            var clean = tag?.Trim();
            if (string.IsNullOrWhiteSpace(clean)) continue;
            if (dna.ForbiddenKeywords.Any(f => clean.Contains(f, StringComparison.OrdinalIgnoreCase))) continue;

            var candidateQuery = dna.Supports3DPrint && !clean.Contains("3d", StringComparison.OrdinalIgnoreCase)
                ? $"{clean} 3d printed"
                : clean;

            if (!queries.Contains(candidateQuery, StringComparer.OrdinalIgnoreCase))
                queries.Add(candidateQuery);

            if (queries.Count >= 4) break;
        }

        if (queries.Count == 0)
            queries.Add(dna.Supports3DPrint ? "3d printed desk decor" : "personalized handmade gift");

        return queries;
    }

    private ResearchCandidate? EvaluateMarketCandidate(
        Guid runId,
        string ownerUserId,
        string shopId,
        ShopDnaProfile dna,
        IReadOnlyList<ShopListingItemDto> ownListings,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        string query,
        MarketSummaryKpisDto kpis,
        MarketListingItemDto item)
    {
        if (ContainsForbiddenOrRejected(item.Title, item.Tags, dna, recentDecisions))
            return null;

        var price = item.PriceUsd > 0 ? item.PriceUsd : kpis.MedianPrice;
        if (price <= 0) price = dna.TargetPriceMinUsd;

        // Filter out items far outside store's target price band
        if (price < dna.TargetPriceMinUsd * 0.6m || price > dna.TargetPriceMaxUsd * 1.6m)
            return null;

        var (netProfit, netMarginPct) = SimulateEtsyNetProfit(price, dna.EstimatedCogsRatio);
        if (netMarginPct < dna.MinAcceptableNetMarginPct)
            return null;

        var cannibalization = _cannibalizationGuard.Check(item.Title, item.Tags, ownListings);
        if (cannibalization.IsBlocked)
            return null;

        var score = _opportunityScorer.Score(new ProductOpportunityInput(
            Title: item.Title,
            Description: item.Description ?? string.Empty,
            Tags: item.Tags,
            Price: price,
            Favorites: item.Favorites,
            Views: item.Views,
            ShopSales: item.ShopSales,
            SeoScore: item.SeoScore,
            Quantity: item.Quantity,
            TargetKeyword: query,
            Category: query));

        if (score.Risk >= 18)
            return null;

        var suggestedTags = Build13Tags(item.Tags, dna.TopPerformingTags);
        var suggestedTitle = TruncateTitle(item.Title, 140);

        return new ResearchCandidate
        {
            CandidateId = Guid.NewGuid(),
            RunId = runId,
            OwnerUserId = ownerUserId,
            ShopId = shopId,
            Title = item.Title,
            SuggestedTitle = suggestedTitle,
            SuggestedTags = suggestedTags,
            SourcePlatform = "EtsyMarket",
            SourceUrl = item.ListingUrl,
            Category = query,
            EstimatedSellPriceUsd = Math.Round(price, 2),
            EstimatedNetProfitUsd = netProfit,
            EstimatedNetMarginPct = netMarginPct,
            CompetitionCount = kpis.TotalListings,
            DemandSignalScore = item.Favorites + (item.EstimatedMonthlySales * 10),
            OpportunityScore = score.Opportunity,
            RiskScore = score.Risk,
            CannibalizationScore = cannibalization.Score,
            CannibalizationWarning = cannibalization.WarningMessage,
            Rationale = $"'{query}' aramasında ${price:F2} fiyat bandında güçlü talep (Tahmini aylık {item.EstimatedMonthlySales} satış, {item.Favorites} favori). Net kâr marjı %{netMarginPct:F1}.",
            EvidenceJson = JsonSerializer.Serialize(new
            {
                etsyListingId = item.Id,
                shopName = item.ShopName,
                marketScore = item.MarketScore,
                medianMarketPrice = kpis.MedianPrice,
                favorites = item.Favorites,
                views = item.Views,
                estimatedMonthlySales = item.EstimatedMonthlySales
            })
        };
    }

    private ResearchCandidate? Evaluate3DCandidate(
        Guid runId,
        string ownerUserId,
        string shopId,
        ShopDnaProfile dna,
        IReadOnlyList<ShopListingItemDto> ownListings,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        EtsyMarketPlace.Domain.Viral3DModels.Entities.Trending3DModel model)
    {
        var tags = model.Tags?.ToList() ?? [];
        if (ContainsForbiddenOrRejected(model.Title, tags, dna, recentDecisions))
            return null;

        var price = Math.Clamp((dna.TargetPriceMinUsd + dna.TargetPriceMaxUsd) / 2m, 14.99m, 89.99m);
        var (netProfit, netMarginPct) = SimulateEtsyNetProfit(price, dna.EstimatedCogsRatio);
        if (netMarginPct < dna.MinAcceptableNetMarginPct)
            return null;

        var cannibalization = _cannibalizationGuard.Check(model.Title, tags, ownListings);
        if (cannibalization.IsBlocked)
            return null;

        var score = _opportunityScorer.Score(new ProductOpportunityInput(
            Title: model.Title,
            Description: model.Description ?? string.Empty,
            Tags: tags,
            Price: price,
            Favorites: model.LikesCount,
            Views: model.TotalDownloads,
            ShopSales: model.PrintsCount,
            SeoScore: 65,
            Quantity: 10,
            TargetKeyword: model.Title,
            Category: model.Category ?? "3D Print"));

        if (score.Risk >= 18)
            return null;

        return new ResearchCandidate
        {
            CandidateId = Guid.NewGuid(),
            RunId = runId,
            OwnerUserId = ownerUserId,
            ShopId = shopId,
            Title = model.Title,
            SuggestedTitle = TruncateTitle($"{model.Title} 3D Printed Decor Gift", 140),
            SuggestedTags = Build13Tags(tags, dna.TopPerformingTags),
            SourcePlatform = model.Platform.ToString(),
            SourceUrl = model.SafeModelUrl,
            Category = model.Category ?? "3D Print",
            EstimatedSellPriceUsd = Math.Round(price, 2),
            EstimatedNetProfitUsd = netProfit,
            EstimatedNetMarginPct = netMarginPct,
            CompetitionCount = Math.Max(0, model.EtsyCompetitionCount),
            DemandSignalScore = model.TotalDownloads + model.LikesCount,
            OpportunityScore = Math.Max(score.Opportunity, model.OpportunityScore),
            RiskScore = score.Risk,
            CannibalizationScore = cannibalization.Score,
            CannibalizationWarning = cannibalization.WarningMessage,
            Rationale = $"{model.Platform} üzerinde {model.TotalDownloads} indirme & ticari lisanslı 3D model. Mağaza 3D baskı DNA'nızla uyumlu.",
            EvidenceJson = JsonSerializer.Serialize(new
            {
                externalId = model.ExternalId,
                platform = model.Platform.ToString(),
                downloads = model.TotalDownloads,
                likes = model.LikesCount,
                license = model.License.LicenseName
            })
        };
    }

    public static (decimal NetProfitUsd, decimal NetMarginPct) SimulateEtsyNetProfit(decimal sellPriceUsd, decimal cogsRatio)
    {
        if (sellPriceUsd <= 0) return (0m, 0m);

        // Etsy standard fee stack: 6.5% transaction + 3% + $0.25 payment + $0.20 listing + 2.5% currency
        var etsyFees = (sellPriceUsd * 0.12m) + 0.45m;
        var cogs = sellPriceUsd * Math.Clamp(cogsRatio, 0.05m, 0.85m);
        var net = Math.Round(sellPriceUsd - etsyFees - cogs, 2);
        var marginPct = Math.Round((net / sellPriceUsd) * 100m, 1);
        return (net, marginPct);
    }

    private static bool ContainsForbiddenOrRejected(
        string title,
        IReadOnlyList<string> tags,
        ShopDnaProfile dna,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions)
    {
        var corpus = $"{title} {string.Join(" ", tags ?? Array.Empty<string>())}";
        if (dna.ForbiddenKeywords.Any(k => !string.IsNullOrWhiteSpace(k) && corpus.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase)))
            return true;

        var rejectedTitles = recentDecisions
            .Where(d => d.Decision == CandidateDecisionStatus.Rejected)
            .Select(d => d.CandidateTitle);

        return rejectedTitles.Any(rt => string.Equals(rt, title, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> Build13Tags(IReadOnlyList<string>? primary, IReadOnlyList<string> fallback)
    {
        var tags = new List<string>();
        foreach (var raw in (primary ?? Array.Empty<string>()).Concat(fallback))
        {
            var clean = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(clean)) continue;
            if (clean.Length > 20) clean = clean[..20].Trim();
            if (clean.Length >= 2 && !tags.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(clean);
            }
            if (tags.Count >= 13) break;
        }
        return tags;
    }

    private static string TruncateTitle(string title, int maxLength)
    {
        var clean = (title ?? string.Empty).Trim();
        return clean.Length <= maxLength ? clean : clean[..maxLength].TrimEnd();
    }
}
