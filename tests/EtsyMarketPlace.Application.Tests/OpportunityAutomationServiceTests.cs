namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Domain.ProductOpportunity;
using Xunit;

public sealed class OpportunityAutomationServiceTests
{
    [Fact]
    public void BuildReportSummarizesDecisionGroupsAndDraftReadyItems()
    {
        var service = new OpportunityAutomationService();

        var report = service.BuildReport(
        [
            Input("Strong item", 82, 76, 12, 80, OpportunityDecisionGroup.StrongOpportunity),
            Input("Risk item", 88, 90, 78, 75, OpportunityDecisionGroup.Risky),
            Input("Weak item", 35, 20, 0, 45, OpportunityDecisionGroup.Weak),
        ]);

        Assert.Equal(3, report.Summary.Total);
        Assert.Equal(1, report.Summary.Strong);
        Assert.Equal(1, report.Summary.Risky);
        Assert.Equal(1, report.Summary.Weak);
        Assert.Equal(1, report.Summary.DraftReady);
        Assert.Equal(1, report.Summary.ManualReview);
        Assert.Equal("Manuel kontrol", report.Items[0].Recommendation.Action);
    }

    [Fact]
    public void RecommendSuggestsTestListForMediumOpportunityOrDemand()
    {
        var service = new OpportunityAutomationService();

        var result = service.Recommend(Input(
            "Medium demand",
            50,
            68,
            10,
            60,
            OpportunityDecisionGroup.WorthTesting));

        Assert.Equal("Test listesine al", result.Action);
        Assert.Equal("Orta", result.Priority);
    }

    private static OpportunityAutomationInput Input(
        string title,
        int opportunity,
        int demand,
        int risk,
        int etsyFit,
        OpportunityDecisionGroup group) =>
        new(
            title,
            opportunity,
            demand,
            risk,
            etsyFit,
            group,
            OpportunityUserStatus.New);
}
