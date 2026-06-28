namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ProductOpportunity;
using EtsyMarketPlace.Domain.ProductOpportunity;
using Xunit;

public sealed class OpportunityDecisionServiceTests
{
    [Fact]
    public void ClassifyMarksHighOpportunityLowRiskAsStrongOpportunity()
    {
        var service = new OpportunityDecisionService();
        var score = CreateScore(opportunity: 82, risk: 20);

        var result = service.Classify(score);

        Assert.Equal(OpportunityDecisionGroup.StrongOpportunity, result.Group);
        Assert.Equal(OpportunityUserStatus.TestList, result.DefaultStatus);
    }

    [Fact]
    public void ClassifyPrioritizesHighRiskOverOpportunity()
    {
        var service = new OpportunityDecisionService();
        var score = CreateScore(opportunity: 88, risk: 78);

        var result = service.Classify(score);

        Assert.Equal(OpportunityDecisionGroup.Risky, result.Group);
        Assert.Equal(OpportunityUserStatus.Watch, result.DefaultStatus);
    }

    [Fact]
    public void MatchesFilterAllowsAllOrSameGroupOnly()
    {
        Assert.True(OpportunityDecisionService.MatchesFilter(
            OpportunityDecisionGroup.All,
            OpportunityDecisionGroup.WorthTesting));
        Assert.True(OpportunityDecisionService.MatchesFilter(
            OpportunityDecisionGroup.WorthTesting,
            OpportunityDecisionGroup.WorthTesting));
        Assert.False(OpportunityDecisionService.MatchesFilter(
            OpportunityDecisionGroup.Risky,
            OpportunityDecisionGroup.Weak));
    }

    private static ProductOpportunityScore CreateScore(int opportunity, int risk) =>
        new(
            opportunity,
            Demand: 75,
            Competition: 35,
            SeoGap: 42,
            PricePotential: 80,
            risk,
            Decision: "",
            new OpportunityScoreBreakdown(
                Demand: 75,
                Competition: 35,
                CompetitionAdvantage: 65,
                SeoGap: 42,
                PricePotential: 80,
                Risk: risk,
                RiskPenalty: risk,
                DemandWeight: 0.34m,
                CompetitionWeight: 0.20m,
                SeoGapWeight: 0.18m,
                PriceWeight: 0.18m,
                RiskWeight: 0.20m,
                AlgorithmVersion: "test",
                Summary: []),
            Reasons: []);
}
