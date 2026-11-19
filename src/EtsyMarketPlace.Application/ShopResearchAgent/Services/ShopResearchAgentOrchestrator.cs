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
/// V2 Personalized autonomous/on-demand product research agent scoped strictly to a single user's Etsy shop.
/// Integrates: Live Ledger Economics (3-scenario Offsite Ads), Multi-Signal Demand Validation,
/// 3-Tier IP Risk Policy (Allow/Review/Block), Hybrid Cannibalization Guard, Closed-Loop Learning (%80 Exploit / %20 Explore),
/// and Scheduled Run Notifications.
/// </summary>
public sealed class ShopResearchAgentOrchestrator
{
    private readonly IShopResearchRepository _repository;
    private readonly IEtsyListingService _listingService;
    private readonly IMarketResearchService _marketResearchService;
    private readonly ShopDnaBuilderService _dnaBuilder;
    private readonly CannibalizationGuardService _cannibalizationGuard;
    private readonly ProductOpportunityScorer _opportunityScorer;
    private readonly ShopEconomicsSimulatorService _economicsSimulator;
    private readonly DemandValidationService _demandValidator;
    private readonly IpRiskPolicyService _ipRiskPolicy;
    private readonly IGeminiListingApiClient? _llmClient;
    private readonly IViral3DModelLakeRepository? _viral3DLake;
    private readonly IShopResearchNotifier? _notifier;

    public ShopResearchAgentOrchestrator(
        IShopResearchRepository repository,
        IEtsyListingService listingService,
        IMarketResearchService marketResearchService,
        ShopDnaBuilderService dnaBuilder,
        CannibalizationGuardService cannibalizationGuard,
        ProductOpportunityScorer opportunityScorer,
        ShopEconomicsSimulatorService? economicsSimulator = null,
        DemandValidationService? demandValidator = null,
        IpRiskPolicyService? ipRiskPolicy = null,
        IGeminiListingApiClient? llmClient = null,
        IViral3DModelLakeRepository? viral3DLake = null,
        IShopResearchNotifier? notifier = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _listingService = listingService ?? throw new ArgumentNullException(nameof(listingService));
        _marketResearchService = marketResearchService ?? throw new ArgumentNullException(nameof(marketResearchService));
        _dnaBuilder = dnaBuilder ?? throw new ArgumentNullException(nameof(dnaBuilder));
        _cannibalizationGuard = cannibalizationGuard ?? throw new ArgumentNullException(nameof(cannibalizationGuard));
        _opportunityScorer = opportunityScorer ?? throw new ArgumentNullException(nameof(opportunityScorer));
        _economicsSimulator = economicsSimulator ?? new ShopEconomicsSimulatorService();
        _demandValidator = demandValidator ?? new DemandValidationService();
        _ipRiskPolicy = ipRiskPolicy ?? new IpRiskPolicyService();
        _llmClient = llmClient;
        _viral3DLake = viral3DLake;
        _notifier = notifier;
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
                // Continue gracefully when live listing snapshot is temporarily unreachable
            }

            var (shopVariableFeeRate, feeSource) = await _economicsSimulator.ResolveShopVariableFeeRateAsync(shopId, ct);
            run.EffectiveShopFeeRatePct = Math.Round(shopVariableFeeRate * 100m, 1);

            var recentDecisions = await _repository.GetRecentDecisionEventsAsync(ownerUserId, shopId, 40, ct);
            var (queries, explorationQuery, hypothesesJson) = await PlanQueriesAsync(dna, recentDecisions, request, ct);
            run.SearchQueriesUsed = queries;
            run.ExplorationQueryUsed = explorationQuery;
            run.HypothesesJson = hypothesesJson;

            var candidates = new List<ResearchCandidate>();
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int attemptedMarketQueries = 0;
            int failedMarketQueries = 0;
            string? lastMarketError = null;

            // Tool 1: Etsy Market Research across Exploitation + 1 Exploration query
            foreach (var q in queries.Take(4))
            {
                ct.ThrowIfCancellationRequested();
                attemptedMarketQueries++;
                MarketSearchResponse marketResult;
                try
                {
                    marketResult = await _marketResearchService.SearchMarketAsync(
                        new MarketSearchQuery(q, Limit: 25, SortBy: "market_score", ShopId: shopId),
                        ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception marketEx)
                {
                    failedMarketQueries++;
                    lastMarketError = marketEx.Message;
                    continue;
                }

                var isExploration = !string.IsNullOrWhiteSpace(explorationQuery)
                    && string.Equals(q, explorationQuery, StringComparison.OrdinalIgnoreCase);

                foreach (var item in marketResult.Listings.OrderByDescending(l => l.MarketScore).Take(10))
                {
                    if (string.IsNullOrWhiteSpace(item.Title) || !seenTitles.Add(item.Title.Trim()))
                        continue;

                    var candidate = EvaluateMarketCandidate(
                        run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions,
                        q, isExploration, shopVariableFeeRate, feeSource, marketResult.Kpis, item);
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
                            run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions,
                            shopVariableFeeRate, feeSource, model);
                        if (candidate != null)
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
                catch
                {
                    // Optional enrichment
                }
            }

            var maxCount = Math.Clamp(request?.MaxCandidates ?? 10, 3, 25);
            var ranked = candidates
                .OrderByDescending(c => c.OpportunityScore)
                .ThenByDescending(c => c.DemandScore)
                .ThenByDescending(c => c.EstimatedNetProfitUsd)
                .Take(maxCount)
                .ToList();

            run.Candidates = ranked;
            run.Status = ResearchRunStatus.Completed;
            run.CompletedAtUtc = DateTime.UtcNow;
            if (ranked.Count > 0)
            {
                run.Summary = $"V2 Tarama Tamamlandı: {queries.Count} sorgu (1 keşif dahil) tarandı; canlı defter kesinti kalibrasyonu (%{run.EffectiveShopFeeRatePct:F1}), talep doğrulaması ve 3'lü telif filtresinden geçen {ranked.Count} ürün adayı bulundu.";
            }
            else if (attemptedMarketQueries > 0 && failedMarketQueries == attemptedMarketQueries)
            {
                run.ErrorMessage = $"Etsy pazar araması başarısız oldu: {lastMarketError}";
                run.Summary = $"V2 Tarama tamamlandı ancak Etsy pazar araması tüm sorgularda hata verdi ({lastMarketError}). Etsy API bağlantınızı/token durumunuzu kontrol edip tekrar deneyin.";
            }
            else
            {
                run.Summary = "V2 Tarama tamamlandı ancak mevcut talep, net kâr marjı, telif ve yamyamlaşma kriterlerini geçen yeni aday bulunamadı.";
            }

            await _repository.SaveRunAsync(run, ct);
            if (triggerType != ResearchTriggerType.Manual)
            {
                await _repository.UpdateLastScheduledRunAsync(ownerUserId, shopId, DateTime.UtcNow, ct);
                if (_notifier != null && ranked.Count > 0)
                {
                    try { await _notifier.NotifyRunCompletedAsync(run, ct); } catch { /* Best-effort notification */ }
                }
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

    private async Task<(IReadOnlyList<string> Queries, string? ExplorationQuery, string HypothesesJson)> PlanQueriesAsync(
        ShopDnaProfile dna,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        RunShopResearchRequest? request,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request?.FocusHint))
        {
            var hint = request.FocusHint.Trim();
            var (fallback, exp) = BuildBanditDeterministicQueries(dna, recentDecisions, hint);
            return (fallback, exp, JsonSerializer.Serialize(new[] { new { query = hint, reason = "Kullanıcı odak ipucu" } }));
        }

        if (_llmClient != null && !string.IsNullOrWhiteSpace(request?.ApiKey))
        {
            try
            {
                var accepted = recentDecisions.Where(d => d.Decision == CandidateDecisionStatus.Accepted || d.Decision == CandidateDecisionStatus.SentToDraft)
                    .Select(d => d.CandidateTitle).Take(6).ToList();
                var rejected = recentDecisions.Where(d => d.Decision == CandidateDecisionStatus.Rejected)
                    .Select(d => $"{d.CandidateTitle} ({d.Reason})").Take(6).ToList();

                var sysPrompt =
                    "Sen uzman bir Etsy ürün araştırma ajanısın. Verilen Mağaza DNA'sına ve geçmiş onay/red hafızasına göre " +
                    "3 adet mağaza nişine uyumlu (exploit) + 1 adet komşu yüksek potansiyelli yeni niş (explore) toplam 4 İngilizce Etsy arama sorgusu üret. " +
                    "Yanıtı YALNIZCA geçerli bir JSON nesnesi olarak ver: {\"queries\":[{\"query\":\"...\",\"mode\":\"exploit|explore\",\"reason\":\"...\"}]}";

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
                    var cleanedContent = StripMarkdownJsonFences(content);
                    using var doc = JsonDocument.Parse(cleanedContent);
                    if (TryExtractQueryArray(doc.RootElement, out var arrayElement))
                    {
                        var list = new List<string>();
                        string? exploration = null;
                        foreach (var el in arrayElement.EnumerateArray())
                        {
                            if (el.ValueKind == JsonValueKind.Object &&
                                TryGetStringPropertyCaseInsensitive(el, "query", out var qRaw) &&
                                !string.IsNullOrWhiteSpace(qRaw))
                            {
                                var qStr = qRaw.Trim();
                                list.Add(qStr);
                                if (TryGetStringPropertyCaseInsensitive(el, "mode", out var mRaw) &&
                                    string.Equals(mRaw, "explore", StringComparison.OrdinalIgnoreCase))
                                {
                                    exploration = qStr;
                                }
                            }
                        }
                        if (list.Count > 0)
                        {
                            var distinct = list.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
                            var resolvedExploration = !string.IsNullOrWhiteSpace(exploration) &&
                                distinct.Contains(exploration, StringComparer.OrdinalIgnoreCase)
                                ? exploration
                                : distinct.Last();
                            return (distinct, resolvedExploration, cleanedContent);
                        }
                    }
                }
            }
            catch
            {
                // Fallback to bandit deterministic planner
            }
        }

        var (deterministic, expQuery) = BuildBanditDeterministicQueries(dna, recentDecisions, null);
        var json = JsonSerializer.Serialize(deterministic.Select(q => new
        {
            query = q,
            mode = string.Equals(q, expQuery, StringComparison.OrdinalIgnoreCase) ? "explore" : "exploit",
            reason = string.Equals(q, expQuery, StringComparison.OrdinalIgnoreCase) ? "Keşif (%20 Exploration)" : "Mağaza DNA + Onay Geçmişi"
        }));
        return (deterministic, expQuery, json);
    }

    private static (IReadOnlyList<string> Queries, string? ExplorationQuery) BuildBanditDeterministicQueries(
        ShopDnaProfile dna,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        string? focusHint)
    {
        var queries = new List<string>();
        if (!string.IsNullOrWhiteSpace(focusHint))
            queries.Add(focusHint.Trim());

        // Boost categories/tags that historically received Accepted/SentToDraft decisions
        var approvedCategories = recentDecisions
            .Where(d => d.Decision == CandidateDecisionStatus.Accepted || d.Decision == CandidateDecisionStatus.SentToDraft)
            .GroupBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k));

        foreach (var seed in approvedCategories.Concat(dna.TopPerformingTags).Concat(dna.CategoryKeywords))
        {
            var clean = seed?.Trim();
            if (string.IsNullOrWhiteSpace(clean)) continue;
            if (dna.ForbiddenKeywords.Any(f => clean.Contains(f, StringComparison.OrdinalIgnoreCase))) continue;

            var candidateQuery = dna.Supports3DPrint && !clean.Contains("3d", StringComparison.OrdinalIgnoreCase)
                ? $"{clean} 3d printed"
                : clean;

            if (!queries.Contains(candidateQuery, StringComparer.OrdinalIgnoreCase))
                queries.Add(candidateQuery);

            if (queries.Count >= 3) break;
        }

        // 4th slot: %20 Exploration query (adjacent high-demand gift/desk/home niche)
        var explorationSeed = dna.Supports3DPrint
            ? "modern desk organizer 3d printed gift"
            : "minimalist handmade home gift";
        if (!queries.Contains(explorationSeed, StringComparer.OrdinalIgnoreCase))
        {
            queries.Add(explorationSeed);
        }

        return (queries, explorationSeed);
    }

    private ResearchCandidate? EvaluateMarketCandidate(
        Guid runId,
        string ownerUserId,
        string shopId,
        ShopDnaProfile dna,
        IReadOnlyList<ShopListingItemDto> ownListings,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        string query,
        bool isExploration,
        decimal shopVariableFeeRate,
        string feeSource,
        MarketSummaryKpisDto kpis,
        MarketListingItemDto item)
    {
        // 1. 3-Tier IP & Trademark Risk Policy (Block -> reject immediately; Review -> keep with warning)
        var ipRisk = _ipRiskPolicy.Evaluate(item.Title, item.Description, item.Tags, dna.ForbiddenKeywords);
        if (ipRisk.Decision == IpRiskDecision.Block)
            return null;

        // 2. Closed-Loop Negative Learning: reject candidates semantically too close to previously rejected items
        if (MatchesRejectedMemory(item.Title, recentDecisions))
            return null;

        var price = item.PriceUsd > 0 ? item.PriceUsd : kpis.MedianPrice;
        if (price <= 0) price = dna.TargetPriceMinUsd;

        if (price < dna.TargetPriceMinUsd * 0.6m || price > dna.TargetPriceMaxUsd * 1.6m)
            return null;

        // 3. V2 Realistic Multi-Scenario Unit Economics
        var econ = _economicsSimulator.Simulate(price, dna.EstimatedCogsRatio, dna.Supports3DPrint, shopVariableFeeRate, feeSource);
        if (econ.NetMarginStandardPct < dna.MinAcceptableNetMarginPct)
            return null;

        // 4. V2 Multi-Signal Demand Validation (blocks dead niches)
        var demand = _demandValidator.EvaluateMarketListing(item, kpis);
        if (demand.IsDeadNiche)
            return null;

        // 5. V2 Hybrid Cannibalization Guard (Trigram Cosine + Token Jaccard + Price Band)
        var cannibalization = _cannibalizationGuard.Check(item.Title, item.Tags, ownListings, price);
        if (cannibalization.IsBlocked)
            return null;

        // 6. Composite V2 Opportunity Score
        var legacyScore = _opportunityScorer.Score(new ProductOpportunityInput(
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

        var effectiveRisk = Math.Max(ipRisk.RiskScore, legacyScore.Risk);
        var marginScore = Math.Clamp((int)Math.Round(econ.NetMarginStandardPct * 1.6m), 0, 100);
        var compositeOpportunity = Math.Clamp(
            (int)Math.Round((demand.DemandScore * 0.42) + (demand.CompetitionAdvantage * 0.23) + (marginScore * 0.25) + ((100 - effectiveRisk) * 0.10)),
            0, 100);

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
            EstimatedSellPriceUsd = econ.SellPriceUsd,
            EstimatedNetProfitUsd = econ.NetProfitStandardUsd,
            EstimatedNetMarginPct = econ.NetMarginStandardPct,
            NetProfitOffsiteAds12Usd = econ.NetProfitOffsiteAds12Usd,
            NetProfitOffsiteAds15Usd = econ.NetProfitOffsiteAds15Usd,
            EffectiveFeeRatePct = econ.EffectiveFeeRatePct,
            CompetitionCount = kpis.TotalListings,
            DemandSignalScore = item.Favorites + (item.EstimatedMonthlySales * 10),
            DemandScore = demand.DemandScore,
            DemandConfidence = demand.Confidence,
            OpportunityScore = compositeOpportunity,
            RiskScore = effectiveRisk,
            RiskDecision = ipRisk.Decision,
            RiskReasons = ipRisk.Reasons,
            CannibalizationScore = cannibalization.Score,
            CannibalizationWarning = cannibalization.WarningMessage,
            IsExplorationCandidate = isExploration,
            Rationale = $"{demand.Summary} Normal Net: ${econ.NetProfitStandardUsd:F2} (%{econ.NetMarginStandardPct:F1}) | Offsite %12: ${econ.NetProfitOffsiteAds12Usd:F2} | Offsite %15: ${econ.NetProfitOffsiteAds15Usd:F2}.",
            EvidenceJson = JsonSerializer.Serialize(new
            {
                etsyListingId = item.Id,
                shopName = item.ShopName,
                marketScore = item.MarketScore,
                medianMarketPrice = kpis.MedianPrice,
                favorites = item.Favorites,
                views = item.Views,
                reviewCount = item.ReviewCount,
                isBestseller = item.IsBestseller,
                estimatedMonthlySales = item.EstimatedMonthlySales,
                feeCalibration = econ.CalibrationSource
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
        decimal shopVariableFeeRate,
        string feeSource,
        EtsyMarketPlace.Domain.Viral3DModels.Entities.Trending3DModel model)
    {
        var tags = model.Tags?.ToList() ?? [];
        var ipRisk = _ipRiskPolicy.Evaluate(model.Title, model.Description, tags, dna.ForbiddenKeywords);
        if (ipRisk.Decision == IpRiskDecision.Block)
            return null;

        if (MatchesRejectedMemory(model.Title, recentDecisions))
            return null;

        var price = Math.Clamp((dna.TargetPriceMinUsd + dna.TargetPriceMaxUsd) / 2m, 14.99m, 89.99m);
        var econ = _economicsSimulator.Simulate(price, dna.EstimatedCogsRatio, is3DPrinted: true, shopVariableFeeRate, feeSource);
        if (econ.NetMarginStandardPct < dna.MinAcceptableNetMarginPct)
            return null;

        var demand = _demandValidator.Evaluate3DModel(model.TotalDownloads, model.LikesCount, model.EtsyCompetitionCount);
        if (demand.IsDeadNiche)
            return null;

        var cannibalization = _cannibalizationGuard.Check(model.Title, tags, ownListings, price);
        if (cannibalization.IsBlocked)
            return null;

        var marginScore = Math.Clamp((int)Math.Round(econ.NetMarginStandardPct * 1.6m), 0, 100);
        var compositeOpportunity = Math.Clamp(
            (int)Math.Round((demand.DemandScore * 0.42) + (demand.CompetitionAdvantage * 0.25) + (marginScore * 0.23) + ((100 - ipRisk.RiskScore) * 0.10)),
            0, 100);

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
            EstimatedSellPriceUsd = econ.SellPriceUsd,
            EstimatedNetProfitUsd = econ.NetProfitStandardUsd,
            EstimatedNetMarginPct = econ.NetMarginStandardPct,
            NetProfitOffsiteAds12Usd = econ.NetProfitOffsiteAds12Usd,
            NetProfitOffsiteAds15Usd = econ.NetProfitOffsiteAds15Usd,
            EffectiveFeeRatePct = econ.EffectiveFeeRatePct,
            CompetitionCount = Math.Max(0, model.EtsyCompetitionCount),
            DemandSignalScore = model.TotalDownloads + model.LikesCount,
            DemandScore = demand.DemandScore,
            DemandConfidence = demand.Confidence,
            OpportunityScore = Math.Max(compositeOpportunity, model.OpportunityScore),
            RiskScore = ipRisk.RiskScore,
            RiskDecision = ipRisk.Decision,
            RiskReasons = ipRisk.Reasons,
            CannibalizationScore = cannibalization.Score,
            CannibalizationWarning = cannibalization.WarningMessage,
            IsExplorationCandidate = false,
            Rationale = $"{demand.Summary} Ticari lisanslı 3D model. Normal Net: ${econ.NetProfitStandardUsd:F2} (%{econ.NetMarginStandardPct:F1}) | Offsite %12: ${econ.NetProfitOffsiteAds12Usd:F2}.",
            EvidenceJson = JsonSerializer.Serialize(new
            {
                externalId = model.ExternalId,
                platform = model.Platform.ToString(),
                downloads = model.TotalDownloads,
                likes = model.LikesCount,
                license = model.License.LicenseName,
                feeCalibration = econ.CalibrationSource
            })
        };
    }

    private bool MatchesRejectedMemory(string candidateTitle, IReadOnlyList<CandidateDecisionEvent> recentDecisions)
    {
        foreach (var decision in recentDecisions.Where(d => d.Decision == CandidateDecisionStatus.Rejected))
        {
            if (string.Equals(decision.CandidateTitle, candidateTitle, StringComparison.OrdinalIgnoreCase))
                return true;

            if (_cannibalizationGuard.ComputeTitleSimilarity(candidateTitle, decision.CandidateTitle) >= 0.65)
                return true;
        }
        return false;
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

    private static bool TryExtractQueryArray(JsonElement root, out JsonElement arrayElement)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            arrayElement = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            string[] preferredKeys = ["queries", "items", "hypotheses", "results"];
            foreach (var key in preferredKeys)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        arrayElement = prop.Value;
                        return true;
                    }
                }
            }

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    arrayElement = prop.Value;
                    return true;
                }
            }
        }

        arrayElement = default;
        return false;
    }

    private static bool TryGetStringPropertyCaseInsensitive(JsonElement obj, string propertyName, out string? value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                prop.Value.ValueKind == JsonValueKind.String)
            {
                value = prop.Value.GetString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string StripMarkdownJsonFences(string raw)
    {
        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(7);
        }
        else if (clean.StartsWith("```", StringComparison.Ordinal))
        {
            clean = clean.Substring(3);
        }

        if (clean.EndsWith("```", StringComparison.Ordinal))
        {
            clean = clean.Substring(0, clean.Length - 3);
        }

        return clean.Trim();
    }
}
