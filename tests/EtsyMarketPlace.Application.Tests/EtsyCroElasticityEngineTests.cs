using EtsyMarketPlace.Application.EtsyIntegration;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public sealed class EtsyCroElasticityEngineTests
{
    [Fact]
    public void CalculateCouponGuardrail_MatchesSethCamachoOrder4225379961_AndFlagsOffsiteAdsStacking()
    {
        // Order #4225379961: $45.00 item + $10.00 shipping, 30% coupon (DSFJY25 -> -$13.50)
        // Customer paid before tax = $41.50, Etsy fees = -$9.15, Net before cost = $32.35 ($35.10 with $2.75 tax)
        var guardrail = EtsyCroElasticityEngine.CalculateCouponGuardrail(
            itemPriceUsd: 45.00m,
            shippingChargedUsd: 10.00m,
            unitTotalCostUsd: 28.00m, // High production + shipping cost to trigger stacking trap when Offsite Ads (15%) hits
            targetMinProfitUsd: 5.00m,
            activeCouponPct: 30.0m,
            exchangeRate: 48.55m);

        Assert.Equal(41.50m, guardrail.CustomerPaidAfterCouponUsd);
        Assert.Equal(9.15m, guardrail.EtsyFeesUsd);
        Assert.True(guardrail.NetProfitStandardUsd > 0m);
        Assert.True(guardrail.NetProfitWithOffsiteAdsUsd < 0m);
        Assert.Equal("CRITICAL_LOSS_TRAP", guardrail.RiskCode);
        Assert.True(guardrail.MaxSafeCouponStandardPct > guardrail.MaxSafeCouponWithOffsiteAdsPct);
    }

    [Fact]
    public void BuildPriceShippingElasticityMatrix_ReturnsFourScenarios_WithFreeShippingBoost()
    {
        var scenarios = EtsyCroElasticityEngine.BuildPriceShippingElasticityMatrix(
            baseItemPriceUsd: 45.00m,
            baseShippingChargedUsd: 10.00m,
            unitTotalCostUsd: 12.50m,
            projectedMonthlyVisits: 600,
            baseConversionRatePct: 2.10m,
            exchangeRate: 50.00m);

        Assert.Equal(4, scenarios.Count);
        var paidBase = scenarios[0];
        var freeShip = scenarios[2];

        Assert.Equal(55.00m, paidBase.CustomerPaysTotalUsd);
        Assert.Equal(55.00m, freeShip.CustomerPaysTotalUsd);
        Assert.Equal(0m, freeShip.ShippingChargedUsd);
        Assert.True(freeShip.EstimatedConversionRatePct > paidBase.EstimatedConversionRatePct);
        Assert.True(freeShip.ExpectedMonthlyNetProfitUsd >= paidBase.ExpectedMonthlyNetProfitUsd);
    }

    [Fact]
    public void AnalyzeProductCroMetrics_ComputesIntentScoreAndFunnelLeakStage()
    {
        var result = EtsyCroElasticityEngine.AnalyzeProductCroMetrics(
            productTitle: "Hexcore Rubik's Cube / Arcane Cosplay",
            views: 420,
            visits: 260,
            favorites: 38,
            estimatedCarts: 16,
            orders: 2,
            avgUnitPriceUsd: 45.00m,
            avgUnitProfitUsd: 21.00m);

        Assert.InRange(result.IntentScore, 18, 98);
        Assert.False(string.IsNullOrWhiteSpace(result.FunnelLeakStage));
        Assert.False(string.IsNullOrWhiteSpace(result.OfferLadderSummary));
        Assert.False(string.IsNullOrWhiteSpace(result.UpsellBundleSuggestion));
        Assert.True(result.MaxSafeCouponPct > 0m);
    }
}
