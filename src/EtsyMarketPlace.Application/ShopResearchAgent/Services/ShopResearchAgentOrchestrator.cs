namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.ProductOpportunity;
using EtsyMarketPlace.Application.SeasonalTrends;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

/// <summary>
/// V3 Personalized on-demand product research agent scoped strictly to a single user's Etsy shop.
/// Integrates: 6-stage SSE refinery funnel, Live Ledger + TR->US Landed Unit Economics,
/// Monopoly/Free-Shipping/Seasonal/Keyword-Momentum Demand Validation, False-Positive Guards,
/// 3-Tier IP Risk Policy, Hybrid Cannibalization Guard, Critic Reflection Loop,
/// Thompson Sampling Bandit Learning, Competitor Shop Mining, 3D Model Lake, Product Blueprints,
/// and exact AI token/cost telemetry.
/// </summary>
public sealed class ShopResearchAgentOrchestrator
{
    public const string UsageModuleName = "Mağaza Araştırma Ajanı";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
    private readonly IMarketKeywordTrackingService? _keywordTrackingService;
    private readonly BanditLearningService _banditLearning;
    private readonly ProductBlueprintGeneratorService _blueprintGenerator;
    private readonly IResearchProgressHub? _progressHub;
    private readonly IAiUsageRecorder? _usageRecorder;

    private sealed class FunnelCounters
    {
        public int ScannedCount { get; set; }
        public int BlockedByIpCount { get; set; }
        public int RejectedByMemoryCount { get; set; }
        public int RejectedByPriceCount { get; set; }
        public int RejectedByMarginCount { get; set; }
        public int RejectedByDeadNicheCount { get; set; }
        public int RejectedByCannibalizationCount { get; set; }
    }

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
        IShopResearchNotifier? notifier = null,
        IMarketKeywordTrackingService? keywordTrackingService = null,
        BanditLearningService? banditLearning = null,
        ProductBlueprintGeneratorService? blueprintGenerator = null,
        IResearchProgressHub? progressHub = null,
        IAiUsageRecorder? usageRecorder = null)
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
        _keywordTrackingService = keywordTrackingService;
        _banditLearning = banditLearning ?? new BanditLearningService(_repository);
        _usageRecorder = usageRecorder;
        _blueprintGenerator = blueprintGenerator ?? new ProductBlueprintGeneratorService(_llmClient, _ipRiskPolicy, _usageRecorder);
        _progressHub = progressHub;
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
            StartedAtUtc = DateTime.UtcNow,
            CriticIterations = 1
        };

        try
        {
            // Stage 1: DNA, Seasonal Context, Keyword Tracking Momentum & Bandit Memory
            PublishProgress(ownerUserId, shopId, run.RunId, "dna", "1. Mağaza DNA & Sezon", 1, 0, 0,
                "Mağaza DNA profili, aktif sezon pencereleri ve geçmiş karar hafızası yükleniyor...");

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
            var banditArms = await LoadBanditArmsSafeAsync(ownerUserId, shopId, ct);
            var (activeSeason, seasonalBoostPct) = ResolveActiveSeason();
            var keywordMomentumMap = await LoadKeywordMomentumMapSafeAsync(ct);

            // Stage 2: Hypothesis & Query Planning (Thompson Bandit + Season + Optional LLM in FullAi mode)
            var aiMode = request?.AiMode ?? ResearchAiMode.Economic;
            PublishProgress(ownerUserId, shopId, run.RunId, "hypotheses", "2. Hipotez & Bandit", 2, 0, 0,
                "Thompson Sampling bandit ağırlıklarıyla mağazaya özel arama hipotezleri planlanıyor...");

            var (queries, explorationQuery, hypothesesJson, planTokens, planCostUsd) = await PlanQueriesAsync(
                dna, recentDecisions, banditArms, activeSeason, request, aiMode, ct);
            run.SearchQueriesUsed = queries;
            run.ExplorationQueryUsed = explorationQuery;
            run.HypothesesJson = hypothesesJson;

            // Stage 3 & 4: Multi-Source Discovery + Rate-Limited Budget + Funnel Filtering
            PublishProgress(ownerUserId, shopId, run.RunId, "discovery", "3. Çoklu Kaynak Keşfi", 3, 0, 0,
                "Etsy pazarı, 3D model gölü ve rakip mağaza boşlukları taranıyor...");

            var budget = new RequestBudget(maxEtsyCalls: 16);
            using var etsyGate = new SemaphoreSlim(2, 2);

            var candidates = new List<ResearchCandidate>();
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var marketTagsByCandidate = new Dictionary<Guid, IReadOnlyList<MarketTagFrequencyDto>>();
            var funnel = new FunnelCounters();
            var marketItemsPool = new List<MarketListingItemDto>();

            int attemptedMarketQueries = 0;
            int failedMarketQueries = 0;
            string? lastMarketError = null;

            // Run 3D Model Lake fetch concurrently with Etsy market queries when enabled
            Task<IReadOnlyList<EtsyMarketPlace.Domain.Viral3DModels.Entities.Trending3DModel>>? lakeTask =
                dna.Supports3DPrint && _viral3DLake != null
                    ? Fetch3DModelsSafeAsync(_viral3DLake, ct)
                    : null;

            foreach (var q in queries.Take(4))
            {
                ct.ThrowIfCancellationRequested();
                if (!budget.TryConsume()) break;

                attemptedMarketQueries++;
                MarketSearchResponse marketResult;
                try
                {
                    marketResult = await SearchMarketGuardedAsync(q, 25, shopId, etsyGate, ct);
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

                marketItemsPool.AddRange(marketResult.Listings);
                var isExploration = !string.IsNullOrWhiteSpace(explorationQuery)
                    && string.Equals(q, explorationQuery, StringComparison.OrdinalIgnoreCase);
                var momentum = ResolveMomentumScore(q, keywordMomentumMap);
                var itemSeasonBoost = ComputeQuerySeasonalBoost(q, activeSeason, seasonalBoostPct);

                foreach (var item in marketResult.Listings.OrderByDescending(l => l.MarketScore).Take(12))
                {
                    if (string.IsNullOrWhiteSpace(item.Title) || !seenTitles.Add(item.Title.Trim()))
                        continue;

                    var candidate = EvaluateMarketCandidate(
                        run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions,
                        q, "EtsyMarket", isExploration, shopVariableFeeRate, feeSource,
                        itemSeasonBoost, momentum, marketResult.Kpis, item, funnel);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                        if (marketResult.Kpis.TopTags.Count > 0)
                        {
                            marketTagsByCandidate[candidate.CandidateId] = marketResult.Kpis.TopTags;
                        }
                    }
                }
            }

            // Fold 3D Model Lake candidates
            if (lakeTask != null)
            {
                var lakeModels = await lakeTask;
                foreach (var model in lakeModels.Where(m => m.License.IsCommercialAllowed).Take(10))
                {
                    if (string.IsNullOrWhiteSpace(model.Title) || !seenTitles.Add(model.Title.Trim()))
                        continue;

                    var candidate = Evaluate3DCandidate(
                        run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions,
                        shopVariableFeeRate, feeSource, model, funnel);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            // Competitor Shop Gap Mining (uses 1 budget call against top rising competitor shop)
            var topCompetitorShop = marketItemsPool
                .Where(m => !string.IsNullOrWhiteSpace(m.ShopName) && m.ShopSales > 0)
                .OrderByDescending(m => m.EstimatedMonthlySales)
                .ThenByDescending(m => m.MarketScore)
                .Select(m => m.ShopName.Trim())
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(topCompetitorShop) && budget.TryConsume())
            {
                try
                {
                    await etsyGate.WaitAsync(ct);
                    CompetitorShopAnalysisDto compAnalysis;
                    try
                    {
                        compAnalysis = await _marketResearchService.AnalyzeCompetitorShopAsync(
                            topCompetitorShop, limit: 10, authShopId: shopId, cancellationToken: ct);
                    }
                    finally
                    {
                        etsyGate.Release();
                    }

                    if (compAnalysis.Found && compAnalysis.Listings.Count > 0)
                    {
                        var compKpis = new MarketSummaryKpisDto
                        {
                            TotalListings = Math.Max(1, compAnalysis.Listings.Count),
                            MedianPrice = compAnalysis.MedianPrice > 0 ? compAnalysis.MedianPrice : compAnalysis.AveragePrice,
                            AveragePrice = compAnalysis.AveragePrice,
                            AverageEstimatedMonthlySales = (double)compAnalysis.EstimatedDailySalesVelocity * 30.0,
                            BestsellerCount = compAnalysis.Shop.BestsellerCount,
                            TopShopName = compAnalysis.Shop.ShopName,
                            TopShopConcentrationPct = 20.0
                        };

                        foreach (var compItem in compAnalysis.Listings.OrderByDescending(l => l.EstimatedMonthlySales).Take(6))
                        {
                            if (string.IsNullOrWhiteSpace(compItem.Title) || !seenTitles.Add(compItem.Title.Trim()))
                                continue;

                            var mapped = new MarketListingItemDto
                            {
                                Id = compItem.Id,
                                Title = compItem.Title,
                                PriceUsd = compItem.PriceUsd,
                                ShopName = compAnalysis.Shop.ShopName,
                                ShopSales = compAnalysis.Shop.TotalSales,
                                ListingUrl = compItem.ListingUrl,
                                ImageUrl = compItem.ImageUrl,
                                ImageUrls = compItem.ImageUrls,
                                Favorites = compItem.Favorites,
                                Views = compItem.Views,
                                SeoScore = compItem.SeoScore,
                                EstimatedMonthlySales = compItem.EstimatedMonthlySales,
                                IsBestseller = compItem.LqsScore >= 85,
                                Tags = compItem.Tags,
                                Description = compItem.Description
                            };

                            var cand = EvaluateMarketCandidate(
                                run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions,
                                compAnalysis.Shop.ShopName, "CompetitorGap", isExploration: false,
                                shopVariableFeeRate, feeSource, seasonalBoostPct: 0, keywordMomentumScore: 55,
                                compKpis, mapped, funnel);
                            if (cand != null)
                            {
                                candidates.Add(cand);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Competitor gap mining is best-effort
                }
            }

            PublishProgress(ownerUserId, shopId, run.RunId, "filtering", "4. Eleme & Birim Ekonomi", 4,
                funnel.ScannedCount, candidates.Count,
                $"{funnel.ScannedCount} ürün tarandı; TR→ABD navlun, telif ve tekel filtrelerinden {candidates.Count} aday geçti.");

            // Stage 5: Critic Reflection Loop (if too few winners and budget remains)
            PublishProgress(ownerUserId, shopId, run.RunId, "critic", "5. Critic Kanıt Denetimi", 5,
                funnel.ScannedCount, candidates.Count,
                candidates.Count < 3 ? "Aday sayısı düşük; alternatif komşu niş sorguları deneniyor..." : "Kanıt yeterliliği doğrulandı.");

            if (candidates.Count < 3 && budget.Remaining >= 2 && attemptedMarketQueries > 0 && failedMarketQueries < attemptedMarketQueries)
            {
                var criticSeeds = BuildCriticRefinementQueries(queries, dna, activeSeason);
                if (criticSeeds.Count > 0)
                {
                    run.CriticIterations = 2;
                    var usedQueries = queries.ToList();
                    foreach (var cq in criticSeeds.Take(2))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!budget.TryConsume()) break;

                        usedQueries.Add(cq);
                        attemptedMarketQueries++;
                        try
                        {
                            var criticMarket = await SearchMarketGuardedAsync(cq, 25, shopId, etsyGate, ct);
                            var momentum = ResolveMomentumScore(cq, keywordMomentumMap);
                            var itemSeasonBoost = ComputeQuerySeasonalBoost(cq, activeSeason, seasonalBoostPct);

                            foreach (var item in criticMarket.Listings.OrderByDescending(l => l.MarketScore).Take(10))
                            {
                                if (string.IsNullOrWhiteSpace(item.Title) || !seenTitles.Add(item.Title.Trim()))
                                    continue;

                                var cand = EvaluateMarketCandidate(
                                    run.RunId, ownerUserId, shopId, dna, ownListings, recentDecisions,
                                    cq, "EtsyMarket", isExploration: true, shopVariableFeeRate, feeSource,
                                    itemSeasonBoost, momentum, criticMarket.Kpis, item, funnel);
                                if (cand != null)
                                {
                                    candidates.Add(cand);
                                    if (criticMarket.Kpis.TopTags.Count > 0)
                                    {
                                        marketTagsByCandidate[cand.CandidateId] = criticMarket.Kpis.TopTags;
                                    }
                                }
                            }
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception marketEx)
                        {
                            failedMarketQueries++;
                            lastMarketError = marketEx.Message;
                        }
                    }
                    run.SearchQueriesUsed = usedQueries;
                }
            }

            var maxCount = Math.Clamp(request?.MaxCandidates ?? 10, 3, 25);
            var ranked = candidates
                .OrderByDescending(c => c.OpportunityScore)
                .ThenByDescending(c => c.DemandScore)
                .ThenByDescending(c => c.EstimatedNetProfitUsd)
                .Take(maxCount)
                .ToList();

            // Stage 6: Product Blueprint Generation for top winners
            PublishProgress(ownerUserId, shopId, run.RunId, "blueprint", "6. Product Blueprint", 6,
                funnel.ScannedCount, ranked.Count,
                $"{ranked.Count} kazanan aday için 55 krk mobil başlık, varyasyon merdiveni ve mockup brifi hazırlanıyor...");

            var useBlueprintAi = aiMode is ResearchAiMode.Balanced or ResearchAiMode.FullAi;
            var (_, _, blueprintTokens, blueprintCostUsd) = await _blueprintGenerator.EnrichTopCandidatesAsync(
                ranked, dna, marketTagsByCandidate, request?.Model, request?.ApiKey, useBlueprintAi, ct);

            var totalAiTokens = planTokens + blueprintTokens;
            var totalAiCostUsd = planCostUsd + blueprintCostUsd;
            var aiSummaryTag = totalAiTokens > 0
                ? $" | AI: {totalAiTokens:N0} token (~${totalAiCostUsd:F4})"
                : " | AI: 0 token (Ekonomik Mod)";

            var telemetry = new ResearchFunnelTelemetry(
                funnel.ScannedCount,
                funnel.BlockedByIpCount,
                funnel.RejectedByMemoryCount,
                funnel.RejectedByPriceCount,
                funnel.RejectedByMarginCount,
                funnel.RejectedByDeadNicheCount,
                funnel.RejectedByCannibalizationCount,
                ranked.Count,
                run.CriticIterations,
                budget.Used,
                budget.MaxEtsyCalls,
                activeSeason?.SeasonName);

            run.FunnelTelemetryJson = JsonSerializer.Serialize(telemetry, JsonOptions);
            run.Candidates = ranked;
            run.Status = ResearchRunStatus.Completed;
            run.CompletedAtUtc = DateTime.UtcNow;

            if (ranked.Count > 0)
            {
                run.Summary = $"V3 Tarama Tamamlandı: {funnel.ScannedCount} ürün incelendi ({run.SearchQueriesUsed.Count} sorgu, {run.CriticIterations} critic turu); " +
                    $"canlı kesinti (%{run.EffectiveShopFeeRatePct:F1}), navlun & tekel doğrulamasından geçen {ranked.Count} ürün blueprint'i hazırlandı.{aiSummaryTag}";
            }
            else if (attemptedMarketQueries > 0 && failedMarketQueries == attemptedMarketQueries)
            {
                run.ErrorMessage = $"Etsy pazar araması başarısız oldu: {lastMarketError}";
                run.Summary = $"V3 Tarama tamamlandı ancak Etsy pazar araması tüm sorgularda hata verdi ({lastMarketError}). Etsy API bağlantınızı/token durumunuzu kontrol edip tekrar deneyin.";
            }
            else
            {
                run.Summary = $"V3 Tarama tamamlandı ({funnel.ScannedCount} ürün tarandı) ancak mevcut talep, net kâr marjı, telif ve yamyamlaşma kriterlerini geçen yeni aday bulunamadı.{aiSummaryTag}";
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

            PublishProgress(ownerUserId, shopId, run.RunId, "completed", "6. Tamamlandı", 6,
                funnel.ScannedCount, ranked.Count, run.Summary);

            return run;
        }
        catch (Exception ex)
        {
            run.Status = ResearchRunStatus.Failed;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.ErrorMessage = ex.Message;
            run.Summary = $"Araştırma çalıştırılırken hata oluştu: {ex.Message}";
            await _repository.SaveRunAsync(run, ct);
            PublishProgress(ownerUserId, shopId, run.RunId, "failed", "Hata", 6, 0, 0, run.Summary);
            return run;
        }
    }

    private async Task<MarketSearchResponse> SearchMarketGuardedAsync(
        string query,
        int limit,
        string shopId,
        SemaphoreSlim gate,
        CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            return await _marketResearchService.SearchMarketAsync(
                new MarketSearchQuery(query, Limit: limit, SortBy: "market_score", ShopId: shopId),
                ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<EtsyMarketPlace.Domain.Viral3DModels.Entities.Trending3DModel>> Fetch3DModelsSafeAsync(
        IViral3DModelLakeRepository lake,
        CancellationToken ct)
    {
        try
        {
            return await lake.GetModelsAsync(30, 0, ct);
        }
        catch
        {
            return Array.Empty<EtsyMarketPlace.Domain.Viral3DModels.Entities.Trending3DModel>();
        }
    }

    private async Task<IReadOnlyList<BanditArmState>> LoadBanditArmsSafeAsync(string ownerUserId, string shopId, CancellationToken ct)
    {
        try
        {
            return await _repository.GetBanditArmsAsync(ownerUserId, shopId, ct);
        }
        catch
        {
            return Array.Empty<BanditArmState>();
        }
    }

    private async Task<Dictionary<string, int>> LoadKeywordMomentumMapSafeAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (_keywordTrackingService == null) return map;

        try
        {
            var tracked = await _keywordTrackingService.GetTrackedKeywordsAsync(ct);
            foreach (var h in tracked)
            {
                int momentum;
                if (h.Latest != null && h.Previous != null)
                {
                    var deltaOpp = (h.Latest.OpportunityScore ?? 50) - (h.Previous.OpportunityScore ?? 50);
                    momentum = Math.Clamp(50 + (deltaOpp * 2), 10, 95);
                }
                else if (h.Latest?.OpportunityScore is int opp)
                {
                    momentum = Math.Clamp(opp, 25, 85);
                }
                else
                {
                    momentum = 50;
                }

                if (!string.IsNullOrWhiteSpace(h.Item.ExternalKey))
                    map[h.Item.ExternalKey.Trim()] = momentum;
                if (!string.IsNullOrWhiteSpace(h.Item.DisplayName))
                    map[h.Item.DisplayName.Trim()] = momentum;
            }
        }
        catch
        {
            // Optional signal
        }

        return map;
    }

    private (SeasonalTrendItem? ActiveSeason, int BoostPct) ResolveActiveSeason()
    {
        try
        {
            var trends = _marketResearchService.GetSeasonalTrends(DateTime.UtcNow);
            if (trends == null || trends.Count == 0) return (null, 0);

            var peak = trends.FirstOrDefault(s => s.Status.Contains("ZİRVE", StringComparison.OrdinalIgnoreCase));
            if (peak != null) return (peak, 25);

            var prep = trends.FirstOrDefault(s => s.Status.Contains("HAZIRLIK", StringComparison.OrdinalIgnoreCase));
            if (prep != null) return (prep, 16);

            return (null, 0);
        }
        catch
        {
            return (null, 0);
        }
    }

    private static int ResolveMomentumScore(string query, Dictionary<string, int> map)
    {
        if (map.Count == 0 || string.IsNullOrWhiteSpace(query)) return 50;
        if (map.TryGetValue(query.Trim(), out var exact)) return exact;

        foreach (var kv in map)
        {
            if (query.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }

        return 50;
    }

    private static int ComputeQuerySeasonalBoost(string query, SeasonalTrendItem? activeSeason, int maxBoostPct)
    {
        if (activeSeason == null || maxBoostPct <= 0 || string.IsNullOrWhiteSpace(query)) return 0;
        if (activeSeason.HighDemandKeywords.Any(k => query.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return maxBoostPct;

        return Math.Max(6, maxBoostPct / 2);
    }

    private void PublishProgress(
        string ownerUserId,
        string shopId,
        Guid runId,
        string stageKey,
        string stageLabel,
        int stageIndex,
        int scannedCount,
        int remainingCount,
        string detail)
    {
        _progressHub?.Publish(ownerUserId, shopId, new ResearchProgressEvent(
            runId,
            stageKey,
            stageLabel,
            stageIndex,
            6,
            scannedCount,
            remainingCount,
            detail,
            DateTime.UtcNow));
    }

    private async Task<(IReadOnlyList<string> Queries, string? ExplorationQuery, string HypothesesJson, int TotalTokens, decimal EstimatedCostUsd)> PlanQueriesAsync(
        ShopDnaProfile dna,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        IReadOnlyList<BanditArmState> banditArms,
        SeasonalTrendItem? activeSeason,
        RunShopResearchRequest? request,
        ResearchAiMode aiMode,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request?.FocusHint))
        {
            var hint = request.FocusHint.Trim();
            var (fallback, exp) = BuildBanditDeterministicQueries(dna, recentDecisions, banditArms, activeSeason, hint);
            return (fallback, exp, JsonSerializer.Serialize(new[] { new { query = hint, reason = "Kullanıcı odak ipucu" } }), 0, 0m);
        }

        var planTokens = 0;
        var planCostUsd = 0m;

        // Only call LLM for query planning when user explicitly chooses FullAi mode
        if (aiMode == ResearchAiMode.FullAi && _llmClient != null && !string.IsNullOrWhiteSpace(request?.ApiKey))
        {
            try
            {
                var accepted = recentDecisions.Where(d => d.Decision == CandidateDecisionStatus.Accepted || d.Decision == CandidateDecisionStatus.SentToDraft)
                    .Select(d => d.CandidateTitle).Take(5).ToList();
                var rejected = recentDecisions.Where(d => d.Decision == CandidateDecisionStatus.Rejected)
                    .Select(d => $"{d.CandidateTitle} ({d.Reason})").Take(5).ToList();

                var sysPrompt =
                    "Sen uzman bir Etsy ürün araştırma ajanısın. Verilen Mağaza DNA'sına, aktif sezon sinyallerine ve geçmiş onay/red hafızasına göre " +
                    "3 adet mağaza nişine uyumlu (exploit) + 1 adet komşu yüksek potansiyelli yeni niş (explore) toplam 4 İngilizce Etsy arama sorgusu üret. " +
                    "Yanıtı YALNIZCA geçerli bir JSON nesnesi olarak ver: {\"queries\":[{\"query\":\"...\",\"mode\":\"exploit|explore\",\"reason\":\"...\"}]}";

                var userPrompt = JsonSerializer.Serialize(new
                {
                    niche = dna.PrimaryNiche,
                    priceBandUsd = $"{dna.TargetPriceMinUsd}-{dna.TargetPriceMaxUsd}",
                    supports3DPrint = dna.Supports3DPrint,
                    topTags = dna.TopPerformingTags.Take(8),
                    forbidden = dna.ForbiddenKeywords.Take(10),
                    activeSeason = activeSeason?.Title,
                    seasonalKeywords = activeSeason?.HighDemandKeywords.Take(4),
                    recentlyAccepted = accepted,
                    recentlyRejected = rejected
                });

                var model = string.IsNullOrWhiteSpace(request.Model) ? "gemini-2.5-flash" : request.Model;
                var llmResult = await _llmClient.GenerateOptimizationJsonWithUsageAsync(
                    sysPrompt, userPrompt, model, request.ApiKey, maxOutputTokens: 500, ct);

                planTokens = llmResult.TotalTokens > 0
                    ? llmResult.TotalTokens
                    : llmResult.PromptTokens + llmResult.CompletionTokens;
                planCostUsd = AiPriceCalculator.CalculateCostUsd(
                    string.IsNullOrWhiteSpace(llmResult.ResolvedModel) ? model : llmResult.ResolvedModel,
                    llmResult.PromptTokens,
                    llmResult.CompletionTokens);

                if (_usageRecorder != null)
                {
                    await _usageRecorder.RecordAsync(UsageModuleName, llmResult, "Sorgu Planlama", ct);
                }

                if (llmResult.Success && !string.IsNullOrWhiteSpace(llmResult.Content))
                {
                    var cleanedContent = StripMarkdownJsonFences(llmResult.Content);
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
                            return (distinct, resolvedExploration, cleanedContent, planTokens, planCostUsd);
                        }
                    }
                }
            }
            catch
            {
                // Fallback to bandit deterministic planner
            }
        }

        var (deterministic, expQuery) = BuildBanditDeterministicQueries(dna, recentDecisions, banditArms, activeSeason, null);
        var json = JsonSerializer.Serialize(deterministic.Select(q => new
        {
            query = q,
            mode = string.Equals(q, expQuery, StringComparison.OrdinalIgnoreCase) ? "explore" : "exploit",
            reason = string.Equals(q, expQuery, StringComparison.OrdinalIgnoreCase) ? "Keşif (%20 Exploration)" : "Thompson Bandit + Mağaza DNA"
        }));
        return (deterministic, expQuery, json, planTokens, planCostUsd);
    }

    private (IReadOnlyList<string> Queries, string? ExplorationQuery) BuildBanditDeterministicQueries(
        ShopDnaProfile dna,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        IReadOnlyList<BanditArmState> banditArms,
        SeasonalTrendItem? activeSeason,
        string? focusHint)
    {
        var queries = new List<string>();
        if (!string.IsNullOrWhiteSpace(focusHint))
            queries.Add(focusHint.Trim());

        var approvedCategories = recentDecisions
            .Where(d => d.Decision == CandidateDecisionStatus.Accepted || d.Decision == CandidateDecisionStatus.SentToDraft)
            .GroupBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToList();

        var rawSeeds = approvedCategories
            .Concat(dna.TopPerformingTags)
            .Concat(dna.CategoryKeywords)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s => !dna.ForbiddenKeywords.Any(f => s.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var rankedSeeds = _banditLearning.RankSeeds(rawSeeds, banditArms);
        foreach (var seed in rankedSeeds)
        {
            var candidateQuery = dna.Supports3DPrint && !seed.Contains("3d", StringComparison.OrdinalIgnoreCase)
                ? $"{seed} 3d printed"
                : seed;

            if (!queries.Contains(candidateQuery, StringComparer.OrdinalIgnoreCase))
                queries.Add(candidateQuery);

            if (queries.Count >= 3) break;
        }

        // 4th slot: Active seasonal seed (if no focus hint) or %20 Exploration seed
        string explorationSeed;
        if (string.IsNullOrWhiteSpace(focusHint) && activeSeason != null && activeSeason.HighDemandKeywords.Count > 0)
        {
            var seasonKw = activeSeason.HighDemandKeywords.FirstOrDefault(k =>
                !dna.ForbiddenKeywords.Any(f => k.Contains(f, StringComparison.OrdinalIgnoreCase))) ?? activeSeason.HighDemandKeywords[0];
            explorationSeed = dna.Supports3DPrint && !seasonKw.Contains("3d", StringComparison.OrdinalIgnoreCase)
                ? $"{seasonKw} 3d printed"
                : seasonKw;
        }
        else
        {
            explorationSeed = dna.Supports3DPrint
                ? "modern desk organizer 3d printed gift"
                : "minimalist handmade home gift";
        }

        if (!queries.Contains(explorationSeed, StringComparer.OrdinalIgnoreCase))
        {
            queries.Add(explorationSeed);
        }

        return (queries, explorationSeed);
    }

    private static List<string> BuildCriticRefinementQueries(
        IReadOnlyList<string> usedQueries,
        ShopDnaProfile dna,
        SeasonalTrendItem? activeSeason)
    {
        var pool = new List<string>();
        if (activeSeason != null)
            pool.AddRange(activeSeason.HighDemandKeywords.Take(2));
        pool.AddRange(dna.CategoryKeywords.Take(3));
        pool.Add(dna.Supports3DPrint ? "3d printed desk decor gift" : "personalized handmade gift");

        return pool
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q.Trim())
            .Where(q => !dna.ForbiddenKeywords.Any(f => q.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Where(q => !usedQueries.Contains(q, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
    }

    private ResearchCandidate? EvaluateMarketCandidate(
        Guid runId,
        string ownerUserId,
        string shopId,
        ShopDnaProfile dna,
        IReadOnlyList<ShopListingItemDto> ownListings,
        IReadOnlyList<CandidateDecisionEvent> recentDecisions,
        string query,
        string sourcePlatform,
        bool isExploration,
        decimal shopVariableFeeRate,
        string feeSource,
        int seasonalBoostPct,
        int keywordMomentumScore,
        MarketSummaryKpisDto kpis,
        MarketListingItemDto item,
        FunnelCounters funnel)
    {
        funnel.ScannedCount++;

        // 1. 3-Tier IP & Trademark Risk Policy
        var ipRisk = _ipRiskPolicy.Evaluate(item.Title, item.Description, item.Tags, dna.ForbiddenKeywords);
        if (ipRisk.Decision == IpRiskDecision.Block)
        {
            funnel.BlockedByIpCount++;
            return null;
        }

        // 2. Closed-Loop Negative Learning
        if (MatchesRejectedMemory(item.Title, recentDecisions))
        {
            funnel.RejectedByMemoryCount++;
            return null;
        }

        var price = item.PriceUsd > 0 ? item.PriceUsd : kpis.MedianPrice;
        if (price <= 0) price = dna.TargetPriceMinUsd;

        if (price < dna.TargetPriceMinUsd * 0.6m || price > dna.TargetPriceMaxUsd * 1.6m)
        {
            funnel.RejectedByPriceCount++;
            return null;
        }

        // 3. V3 Landed Unit Economics (Standard + TR->US International Shipping, Packaging, Return Reserve)
        var econ = _economicsSimulator.SimulateLanded(
            price,
            dna.EstimatedCogsRatio,
            dna.Supports3DPrint,
            shopVariableFeeRate,
            feeSource,
            dna.DefaultShippingCostUsd,
            dna.DefaultPackagingCostUsd,
            dna.ReturnReserveRatePct);

        var gatingMargin = dna.DefaultShippingCostUsd > 0m ? econ.NetMarginLandedPct : econ.NetMarginStandardPct;
        if (gatingMargin < dna.MinAcceptableNetMarginPct)
        {
            funnel.RejectedByMarginCount++;
            return null;
        }

        // 4. V3 Demand + Monopoly + Seasonal + Momentum Validation
        var demand = _demandValidator.EvaluateMarketListing(item, kpis, seasonalBoostPct, keywordMomentumScore);
        if (demand.IsDeadNiche)
        {
            funnel.RejectedByDeadNicheCount++;
            return null;
        }

        // 5. V2/V3 Hybrid Cannibalization Guard
        var cannibalization = _cannibalizationGuard.Check(item.Title, item.Tags, ownListings, price);
        if (cannibalization.IsBlocked)
        {
            funnel.RejectedByCannibalizationCount++;
            return null;
        }

        // 6. Composite V3 Opportunity Score
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
        var activeNetProfit = dna.DefaultShippingCostUsd > 0m ? econ.NetProfitLandedUsd : econ.NetProfitStandardUsd;
        var activeNetMarginPct = dna.DefaultShippingCostUsd > 0m ? econ.NetMarginLandedPct : econ.NetMarginStandardPct;
        var marginScore = Math.Clamp((int)Math.Round(activeNetMarginPct * 1.6m), 0, 100);
        var compositeOpportunity = Math.Clamp(
            (int)Math.Round((demand.DemandScore * 0.40) + (demand.CompetitionAdvantage * 0.25) + (marginScore * 0.25) + ((100 - effectiveRisk) * 0.10)),
            0, 100);

        var warningFlags = (demand.WarningFlags ?? Array.Empty<string>()).ToList();
        if (dna.DefaultShippingCostUsd == 0m && econ.NetMarginLandedPct < 15m && !warningFlags.Contains("FreeShippingMarginRisk"))
        {
            warningFlags.Add("FreeShippingMarginRisk");
        }

        var imageUrl = !string.IsNullOrWhiteSpace(item.ImageUrl)
            ? item.ImageUrl.Trim()
            : item.ImageUrls.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))?.Trim() ?? string.Empty;

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
            SourcePlatform = sourcePlatform,
            SourceUrl = item.ListingUrl,
            ImageUrl = imageUrl,
            Category = query,
            EstimatedSellPriceUsd = econ.SellPriceUsd,
            EstimatedNetProfitUsd = activeNetProfit,
            EstimatedNetMarginPct = activeNetMarginPct,
            EstimatedShippingUsd = econ.EstimatedShippingUsd,
            EstimatedPackagingUsd = econ.PackagingUsd,
            EstimatedReturnReserveUsd = econ.ReturnReserveUsd,
            EstimatedCogsUsd = econ.EffectiveCogsUsd,
            NetProfitOffsiteAds12Usd = econ.NetProfitOffsiteAds12Usd,
            NetProfitOffsiteAds15Usd = econ.NetProfitOffsiteAds15Usd,
            EffectiveFeeRatePct = econ.EffectiveFeeRatePct,
            CompetitionCount = kpis.TotalListings,
            TopShopConcentrationPct = (decimal)Math.Round(kpis.TopShopConcentrationPct, 1),
            FreeShippingRatioPct = (decimal)Math.Round(kpis.FreeShippingRatioPct, 1),
            SeasonalBoostPct = demand.SeasonalBoostPct,
            MomentumScore = demand.MomentumScore,
            DemandSignalScore = item.Favorites + (item.EstimatedMonthlySales * 10),
            DemandScore = demand.DemandScore,
            DemandConfidence = demand.Confidence,
            OpportunityScore = compositeOpportunity,
            RiskScore = effectiveRisk,
            RiskDecision = ipRisk.Decision,
            RiskReasons = ipRisk.Reasons,
            WarningFlags = warningFlags,
            CannibalizationScore = cannibalization.Score,
            CannibalizationWarning = cannibalization.WarningMessage,
            IsExplorationCandidate = isExploration,
            Rationale = $"{demand.Summary} Normal Net: ${econ.NetProfitStandardUsd:F2} (%{econ.NetMarginStandardPct:F1}) | TR→ABD Landed Net: ${econ.NetProfitLandedUsd:F2} (%{econ.NetMarginLandedPct:F1}, Navlun ~${econ.EstimatedShippingUsd:F2}) | Offsite %12: ${econ.NetProfitOffsiteAds12Usd:F2}.",
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
                topShopConcentrationPct = kpis.TopShopConcentrationPct,
                freeShippingRatioPct = kpis.FreeShippingRatioPct,
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
        EtsyMarketPlace.Domain.Viral3DModels.Entities.Trending3DModel model,
        FunnelCounters funnel)
    {
        funnel.ScannedCount++;
        var tags = model.Tags?.ToList() ?? [];
        var ipRisk = _ipRiskPolicy.Evaluate(model.Title, model.Description, tags, dna.ForbiddenKeywords);
        if (ipRisk.Decision == IpRiskDecision.Block)
        {
            funnel.BlockedByIpCount++;
            return null;
        }

        if (MatchesRejectedMemory(model.Title, recentDecisions))
        {
            funnel.RejectedByMemoryCount++;
            return null;
        }

        var price = Math.Clamp((dna.TargetPriceMinUsd + dna.TargetPriceMaxUsd) / 2m, 14.99m, 89.99m);
        var econ = _economicsSimulator.SimulateLanded(
            price,
            dna.EstimatedCogsRatio,
            is3DPrinted: true,
            shopVariableFeeRate,
            feeSource,
            dna.DefaultShippingCostUsd,
            dna.DefaultPackagingCostUsd,
            dna.ReturnReserveRatePct);

        var gatingMargin = dna.DefaultShippingCostUsd > 0m ? econ.NetMarginLandedPct : econ.NetMarginStandardPct;
        if (gatingMargin < dna.MinAcceptableNetMarginPct)
        {
            funnel.RejectedByMarginCount++;
            return null;
        }

        var demand = _demandValidator.Evaluate3DModel(model.TotalDownloads, model.LikesCount, model.EtsyCompetitionCount);
        if (demand.IsDeadNiche)
        {
            funnel.RejectedByDeadNicheCount++;
            return null;
        }

        var cannibalization = _cannibalizationGuard.Check(model.Title, tags, ownListings, price);
        if (cannibalization.IsBlocked)
        {
            funnel.RejectedByCannibalizationCount++;
            return null;
        }

        var activeNetProfit = dna.DefaultShippingCostUsd > 0m ? econ.NetProfitLandedUsd : econ.NetProfitStandardUsd;
        var activeNetMarginPct = dna.DefaultShippingCostUsd > 0m ? econ.NetMarginLandedPct : econ.NetMarginStandardPct;
        var marginScore = Math.Clamp((int)Math.Round(activeNetMarginPct * 1.6m), 0, 100);
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
            ImageUrl = model.PrimaryImageUrl ?? string.Empty,
            Category = model.Category ?? "3D Print",
            EstimatedSellPriceUsd = econ.SellPriceUsd,
            EstimatedNetProfitUsd = activeNetProfit,
            EstimatedNetMarginPct = activeNetMarginPct,
            EstimatedShippingUsd = econ.EstimatedShippingUsd,
            EstimatedPackagingUsd = econ.PackagingUsd,
            EstimatedReturnReserveUsd = econ.ReturnReserveUsd,
            EstimatedCogsUsd = econ.EffectiveCogsUsd,
            NetProfitOffsiteAds12Usd = econ.NetProfitOffsiteAds12Usd,
            NetProfitOffsiteAds15Usd = econ.NetProfitOffsiteAds15Usd,
            EffectiveFeeRatePct = econ.EffectiveFeeRatePct,
            CompetitionCount = Math.Max(0, model.EtsyCompetitionCount),
            MomentumScore = demand.MomentumScore,
            DemandSignalScore = model.TotalDownloads + model.LikesCount,
            DemandScore = demand.DemandScore,
            DemandConfidence = demand.Confidence,
            OpportunityScore = Math.Max(compositeOpportunity, model.OpportunityScore),
            RiskScore = ipRisk.RiskScore,
            RiskDecision = ipRisk.Decision,
            RiskReasons = ipRisk.Reasons,
            WarningFlags = dna.DefaultShippingCostUsd == 0m && econ.NetMarginLandedPct < 15m ? ["FreeShippingMarginRisk"] : Array.Empty<string>(),
            CannibalizationScore = cannibalization.Score,
            CannibalizationWarning = cannibalization.WarningMessage,
            IsExplorationCandidate = false,
            Rationale = $"{demand.Summary} Ticari lisanslı 3D model. Normal Net: ${econ.NetProfitStandardUsd:F2} (%{econ.NetMarginStandardPct:F1}) | TR→ABD Landed Net: ${econ.NetProfitLandedUsd:F2} (Navlun ~${econ.EstimatedShippingUsd:F2}).",
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
