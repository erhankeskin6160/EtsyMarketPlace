namespace EtsyMarketPlace.Application.Tests.ShopResearchAgent;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.ShopResearchAgent.Services;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using EtsyMarketPlace.Infrastructure.ShopResearchAgent;
using Xunit;

public sealed class ShopResearchAgentV2Tests
{
    [Fact]
    public void EconomicsSimulator_Computes3Scenarios_AndApplies3DFailureBuffer()
    {
        var sim = new ShopEconomicsSimulatorService();
        var standard = sim.Simulate(sellPriceUsd: 40m, cogsRatio: 0.35m, is3DPrinted: false, variableFeeRate: 0.131m, "test");
        var printed3D = sim.Simulate(sellPriceUsd: 40m, cogsRatio: 0.35m, is3DPrinted: true, variableFeeRate: 0.131m, "test");

        Assert.True(standard.NetProfitStandardUsd > printed3D.NetProfitStandardUsd);
        Assert.True(standard.NetProfitOffsiteAds12Usd < standard.NetProfitStandardUsd);
        Assert.True(standard.NetProfitOffsiteAds15Usd < standard.NetProfitOffsiteAds12Usd);
        Assert.Equal(Math.Round(standard.NetProfitStandardUsd - (40m * 0.15m), 2), standard.NetProfitOffsiteAds15Usd);
    }

    [Fact]
    public void DemandValidation_FlagsDeadNiche_AndScoresHighDemandBestseller()
    {
        var validator = new DemandValidationService();
        var dead = validator.EvaluateMarketListing(
            new MarketListingItemDto { EstimatedMonthlySales = 0, Favorites = 2, Views = 40, ReviewCount = 0, IsBestseller = false },
            new MarketSummaryKpisDto { TotalListings = 50, AverageEstimatedMonthlySales = 0.2, BestsellerCount = 0 });

        var hot = validator.EvaluateMarketListing(
            new MarketListingItemDto { EstimatedMonthlySales = 30, Favorites = 650, Views = 3000, ReviewCount = 120, IsBestseller = true },
            new MarketSummaryKpisDto { TotalListings = 300, AverageEstimatedMonthlySales = 12, BestsellerCount = 25 });

        Assert.True(dead.IsDeadNiche);
        Assert.Equal(DemandConfidenceLevel.Low, dead.Confidence);
        Assert.False(hot.IsDeadNiche);
        Assert.Equal(DemandConfidenceLevel.High, hot.Confidence);
        Assert.True(hot.DemandScore >= 65);
    }

    [Fact]
    public void IpRiskPolicy_ReturnsBlockReviewAndAllowProperly()
    {
        var policy = new IpRiskPolicyService();

        var blocked = policy.Evaluate("Pokemon Pikachu 3D Printed Lamp", null, ["pikachu", "lamp"]);
        var review = policy.Evaluate("Fantasy Dragon Inspired By Medieval Art", null, ["dragon", "decor"]);
        var clean = policy.Evaluate("Minimalist Walnut Desk Pen Holder", null, ["desk organizer", "walnut"]);

        Assert.Equal(IpRiskDecision.Block, blocked.Decision);
        Assert.True(blocked.RiskScore >= 75);

        Assert.Equal(IpRiskDecision.Review, review.Decision);
        Assert.NotEmpty(review.Reasons);

        Assert.Equal(IpRiskDecision.Allow, clean.Decision);
        Assert.Equal(0, clean.RiskScore);
    }

    [Fact]
    public void HybridCannibalization_DetectsMorphologicalVariationWithTrigrams()
    {
        var guard = new CannibalizationGuardService();
        var sim = guard.ComputeTitleSimilarity("Articulated Crystal Dragon Toy", "Articulated Crystal Dragons Toys");

        Assert.True(sim >= 0.70, $"Expected high trigram+token similarity, got {sim:F2}");
    }

    [Fact]
    public async Task SqliteRepository_PersistsAndReadsV2Fields()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"shop_research_v2_test_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteShopResearchRepository(dbPath);
            await repo.InitializeAsync();

            var runId = Guid.NewGuid();
            var cand = new ResearchCandidate
            {
                CandidateId = Guid.NewGuid(),
                RunId = runId,
                OwnerUserId = "owner-v2",
                ShopId = "53236321",
                Title = "Walnut Cable Stand",
                SuggestedTitle = "Walnut Cable Stand Desk Organizer",
                SuggestedTags = ["cable stand", "desk organizer"],
                EstimatedSellPriceUsd = 38m,
                EstimatedNetProfitUsd = 14.5m,
                EstimatedNetMarginPct = 38.2m,
                NetProfitOffsiteAds12Usd = 9.94m,
                NetProfitOffsiteAds15Usd = 8.80m,
                EffectiveFeeRatePct = 14.3m,
                DemandScore = 78,
                DemandConfidence = DemandConfidenceLevel.High,
                RiskDecision = IpRiskDecision.Review,
                RiskReasons = ["Manuel kontrol önerilir"],
                IsExplorationCandidate = true
            };

            var run = new ResearchRun
            {
                RunId = runId,
                OwnerUserId = "owner-v2",
                ShopId = "53236321",
                TriggerType = ResearchTriggerType.Daily,
                Status = ResearchRunStatus.Completed,
                ExplorationQueryUsed = "minimalist desk gift",
                EffectiveShopFeeRatePct = 14.3m,
                Summary = "V2 Test",
                Candidates = [cand]
            };

            await repo.SaveRunAsync(run);
            var loaded = await repo.GetRunByIdAsync("owner-v2", "53236321", runId);

            Assert.NotNull(loaded);
            Assert.Equal("minimalist desk gift", loaded!.ExplorationQueryUsed);
            Assert.Equal(14.3m, loaded.EffectiveShopFeeRatePct);
            Assert.Single(loaded.Candidates);
            Assert.Equal(9.94m, loaded.Candidates[0].NetProfitOffsiteAds12Usd);
            Assert.Equal(8.80m, loaded.Candidates[0].NetProfitOffsiteAds15Usd);
            Assert.Equal(78, loaded.Candidates[0].DemandScore);
            Assert.Equal(DemandConfidenceLevel.High, loaded.Candidates[0].DemandConfidence);
            Assert.Equal(IpRiskDecision.Review, loaded.Candidates[0].RiskDecision);
            Assert.True(loaded.Candidates[0].IsExplorationCandidate);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }
}
