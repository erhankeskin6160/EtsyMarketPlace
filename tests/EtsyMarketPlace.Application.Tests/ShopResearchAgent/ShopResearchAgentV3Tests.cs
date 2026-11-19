namespace EtsyMarketPlace.Application.Tests.ShopResearchAgent;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.ProductOpportunity;
using EtsyMarketPlace.Application.SeasonalTrends;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Application.ShopResearchAgent.Services;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using EtsyMarketPlace.Infrastructure.ShopResearchAgent;
using Xunit;

public sealed class ShopResearchAgentV3Tests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void EconomicsSimulator_SimulateLanded_ComputesShippingPackagingAndReturnReserve()
    {
        var sim = new ShopEconomicsSimulatorService();
        var landed = sim.SimulateLanded(
            sellPriceUsd: 40m,
            cogsRatio: 0.30m,
            is3DPrinted: true,
            variableFeeRate: 0.131m,
            calibrationSource: "test",
            configuredShippingUsd: 10m,
            packagingUsd: 1.50m,
            returnReserveRatePct: 5.0m);

        Assert.Equal(10m, landed.EstimatedShippingUsd);
        Assert.Equal(1.50m, landed.PackagingUsd);
        Assert.True(landed.ReturnReserveUsd > 0m);
        Assert.True(landed.NetProfitLandedUsd < landed.NetProfitStandardUsd);
        Assert.Equal(Math.Round(landed.NetProfitLandedUsd - (40m * 0.12m), 2), landed.NetProfitOffsiteAds12Usd);
    }

    [Fact]
    public void DemandValidation_FlagsRedOceanSeasonalPeakLossLeader_AndAppliesMonopolyPenalty()
    {
        var validator = new DemandValidationService();
        var res = validator.EvaluateMarketListing(
            new MarketListingItemDto
            {
                PriceUsd = 20m,
                EstimatedMonthlySales = 10,
                Favorites = 150,
                Views = 1200,
                ReviewCount = 30
            },
            new MarketSummaryKpisDto
            {
                TotalListings = 8000,
                MedianPrice = 40m,
                AverageEstimatedMonthlySales = 1.5,
                TopShopConcentrationPct = 58.0,
                FreeShippingRatioPct = 75.0
            },
            seasonalBoostPct: 25,
            keywordMomentumScore: 70);

        Assert.Equal(22, res.MonopolyPenalty);
        Assert.NotNull(res.WarningFlags);
        Assert.Contains("HighMonopoly", res.WarningFlags!);
        Assert.Contains("FreeShippingPressure", res.WarningFlags!);
        Assert.Contains("RedOcean", res.WarningFlags!);
        Assert.Contains("SeasonalPeak", res.WarningFlags!);
        Assert.Contains("LossLeader", res.WarningFlags!);
    }

    [Fact]
    public async Task BanditLearning_UpdatesBetaArmsWithDecay_AndPrefersAcceptedArm()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"shop_research_v3_bandit_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteShopResearchRepository(dbPath);
            await repo.InitializeAsync();
            var bandit = new BanditLearningService(repo);
            var now = DateTime.UtcNow;

            for (var i = 0; i < 4; i++)
            {
                await bandit.RecordOutcomeAsync("user-b", "53236321", "desk organizer", CandidateDecisionStatus.Accepted, CancellationToken.None, now);
                await bandit.RecordOutcomeAsync("user-b", "53236321", "phone case", CandidateDecisionStatus.Rejected, CancellationToken.None, now);
            }

            var arms = await repo.GetBanditArmsAsync("user-b", "53236321");
            var goodArm = arms.Single(a => a.ArmKey == "desk organizer");
            var badArm = arms.Single(a => a.ArmKey == "phone case");

            Assert.True(goodArm.Alpha > goodArm.Beta);
            Assert.True(badArm.Beta > badArm.Alpha);

            var ranked = bandit.RankSeeds(["phone case", "desk organizer"], arms, new Random(42));
            Assert.Equal("desk organizer", ranked[0]);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    [Fact]
    public void BlueprintGenerator_Produces55CharMobileTitle_13Tags_And3TierVariationLadder()
    {
        var generator = new ProductBlueprintGeneratorService();
        var bp = generator.BuildDeterministicBlueprint(
            title: "Free Shipping Articulated Geometric Planter Pot Best Seller 3D Printed Decor Gift",
            basePriceUsd: 40m,
            rawTags: ["planter pot", "geometric vase", "modern desk decor", "minimalist planter"],
            shopTopTags: ["3d printed gift", "office decor", "succulent pot", "housewarming gift", "pla planter", "desk accent", "cute pot", "eco decor", "gift for mom", "scandinavian decor"],
            marketTopTags:
            [
                new MarketTagFrequencyDto { Tag = "modern succulent pot", Count = 12, WordCount = 3, CompetitionLevel = "Düşük" },
                new MarketTagFrequencyDto { Tag = "too long tag over twenty chars", Count = 8, WordCount = 5, CompetitionLevel = "Düşük" }
            ],
            preferredMaterials: ["PLA", "Matte Filament"],
            supports3DPrint: true,
            category: "planter",
            topShopConcentrationPct: 18m,
            freeShippingRatioPct: 50m,
            forbiddenKeywords: ["disney"]);

        Assert.InRange(bp.MobileTitle55.Length, 5, 55);
        Assert.DoesNotContain("Free Shipping", bp.MobileTitle55, StringComparison.OrdinalIgnoreCase);
        Assert.InRange(bp.FullTitle140.Length, bp.MobileTitle55.Length, 140);
        Assert.InRange(bp.LongTailTags.Count, 4, 13);
        Assert.All(bp.LongTailTags, t => Assert.True(t.Length <= 20));
        Assert.Equal(3, bp.VariationLadder.Count);
        Assert.Equal("Giriş", bp.VariationLadder[0].TierName);
        Assert.Equal("Ana", bp.VariationLadder[1].TierName);
        Assert.Equal("Premium Set", bp.VariationLadder[2].TierName);
        Assert.True(bp.VariationLadder[0].PriceUsd < bp.VariationLadder[1].PriceUsd);
        Assert.True(bp.VariationLadder[1].PriceUsd < bp.VariationLadder[2].PriceUsd);
    }

    [Fact]
    public async Task OrchestratorV3_PopulatesFunnelTelemetry_ImageUrl_Blueprint_AndCriticRefinement()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"shop_research_v3_orch_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteShopResearchRepository(dbPath);
            await repo.InitializeAsync();
            var hub = new InMemoryResearchProgressHub();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var events = new List<ResearchProgressEvent>();
            var subTask = Task.Run(async () =>
            {
                await foreach (var evt in hub.SubscribeAsync("owner-v3", "53236321", cts.Token))
                {
                    events.Add(evt);
                    if (evt.StageKey == "completed") break;
                }
            });

            // Wait briefly so subscription registers before run starts
            await Task.Delay(50);

            var marketStub = new CriticRefinementStubMarketService();
            var orchestrator = new ShopResearchAgentOrchestrator(
                repo,
                new EmptyListingService(),
                marketStub,
                new ShopDnaBuilderService(),
                new CannibalizationGuardService(),
                new ProductOpportunityScorer(),
                progressHub: hub);

            var run = await orchestrator.RunResearchAsync("owner-v3", "53236321", ResearchTriggerType.Manual);
            await subTask;

            Assert.Equal(ResearchRunStatus.Completed, run.Status);
            Assert.Equal(2, run.CriticIterations);
            Assert.NotEmpty(run.Candidates);

            var telemetry = JsonSerializer.Deserialize<ResearchFunnelTelemetry>(run.FunnelTelemetryJson, JsonOptions);
            Assert.NotNull(telemetry);
            Assert.True(telemetry!.ScannedCount >= 3);
            Assert.True(telemetry.BlockedByIpCount >= 1);
            Assert.Equal(run.Candidates.Count, telemetry.WinningCount);
            Assert.InRange(telemetry.EtsyCallsUsed, 1, telemetry.EtsyCallBudget);

            var winner = run.Candidates[0];
            Assert.False(string.IsNullOrWhiteSpace(winner.ImageUrl));
            Assert.InRange(winner.MobileTitle55.Length, 5, 55);
            Assert.InRange(winner.SuggestedTitle.Length, 5, 140);
            Assert.True(winner.EstimatedShippingUsd > 0m);

            var bp = JsonSerializer.Deserialize<ProductBlueprintDto>(winner.BlueprintJson, JsonOptions);
            Assert.NotNull(bp);
            Assert.Equal(3, bp!.VariationLadder.Count);

            Assert.Contains(events, e => e.StageKey == "dna");
            Assert.Contains(events, e => e.StageKey == "completed");
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    private sealed class EmptyListingService : IEtsyListingService
    {
        public Task<IReadOnlyList<ShopListingItemDto>> GetShopListingsAsync(string shopId, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ShopListingItemDto>>(Array.Empty<ShopListingItemDto>());

        public Task<ListingUpdateResultDto> UpdateListingAsync(string shopId, string listingId, UpdateListingRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ListingUpdateResultDto> UpdateListingStateAsync(string shopId, long listingId, string state, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class CriticRefinementStubMarketService : IMarketResearchService
    {
        private int _callCount;

        public Task<MarketSearchResponse> SearchMarketAsync(MarketSearchQuery query, CancellationToken cancellationToken = default)
        {
            var idx = Interlocked.Increment(ref _callCount);
            var listings = idx == 1
                ? new List<MarketListingItemDto>
                {
                    new()
                    {
                        Id = 10,
                        Title = "Disney Marvel Spider-Man Mug",
                        PriceUsd = 35m,
                        Favorites = 500,
                        EstimatedMonthlySales = 20,
                        MarketScore = 90
                    },
                    new()
                    {
                        Id = 11,
                        Title = "Minimalist Walnut Headphone Stand",
                        PriceUsd = 38m,
                        Favorites = 320,
                        Views = 1800,
                        ReviewCount = 45,
                        EstimatedMonthlySales = 18,
                        MarketScore = 88,
                        ImageUrl = "https://i.etsystatic.com/sample-1.jpg",
                        ListingUrl = "https://www.etsy.com/listing/11",
                        Tags = ["headphone stand", "walnut desk", "gaming gift"]
                    }
                }
                : new List<MarketListingItemDto>
                {
                    new()
                    {
                        Id = 20 + idx,
                        Title = $"Geometric Ribbed Desk Lamp Edition {idx}",
                        PriceUsd = 42m,
                        Favorites = 280,
                        Views = 1500,
                        ReviewCount = 35,
                        EstimatedMonthlySales = 16,
                        MarketScore = 86,
                        ImageUrl = $"https://i.etsystatic.com/sample-{idx}.jpg",
                        ListingUrl = $"https://www.etsy.com/listing/{20 + idx}",
                        Tags = ["desk lamp", "nordic decor", "gift idea"]
                    }
                };

            return Task.FromResult(new MarketSearchResponse
            {
                Keyword = query.Keyword,
                Total = 240,
                Kpis = new MarketSummaryKpisDto
                {
                    TotalListings = 240,
                    MedianPrice = 38m,
                    AveragePrice = 39m,
                    TopShopConcentrationPct = 18.5,
                    FreeShippingRatioPct = 55.0,
                    AverageEstimatedMonthlySales = 8.0,
                    BestsellerCount = 12
                },
                Listings = listings
            });
        }

        public Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(MarketAiReportCommand command, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<CompetitorShopAnalysisDto> AnalyzeCompetitorShopAsync(string shopNameOrUrl, int limit = 50, string authShopId = "53236321", CancellationToken cancellationToken = default)
            => Task.FromResult(new CompetitorShopAnalysisDto());

        public IReadOnlyList<SeasonalTrendItem> GetSeasonalTrends(DateTime? referenceDate = null)
            =>
            [
                new SeasonalTrendItem(
                    "test_q4",
                    "🎄",
                    "Yılbaşı Hediye Dalgası",
                    "Q4 Holiday",
                    "Ekim - Aralık",
                    "🔥 ZİRVE SEZON",
                    "#10b981",
                    2,
                    ["personalized desk gift", "holiday organizer"],
                    ["personalized gift"],
                    ["Masaüstü Dekor"],
                    "Test")
            ];
    }
}
