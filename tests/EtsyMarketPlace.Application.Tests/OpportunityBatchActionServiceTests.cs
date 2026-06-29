namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Domain.ProductOpportunity;
using Xunit;

public sealed class OpportunityBatchActionServiceTests
{
    [Fact]
    public void GenerateAiDraftBlocksRiskyItemsWithoutApproval()
    {
        var service = new OpportunityBatchActionService();

        var result = service.Apply(new OpportunityBatchActionRequest(
            OpportunityBatchActionType.GenerateAiDraft,
            [Input("Risky", 80, 75, 82, 70, OpportunityDecisionGroup.Risky)],
            AllowRiskyAiDraft: false));

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.BlockedCount);
        Assert.False(result.Items[0].Success);
        Assert.Equal(OpportunityUserStatus.New, result.Items[0].NewStatus);
    }

    [Fact]
    public void GenerateAiDraftAllowsRiskyItemsAfterExplicitApproval()
    {
        var service = new OpportunityBatchActionService();

        var result = service.Apply(new OpportunityBatchActionRequest(
            OpportunityBatchActionType.GenerateAiDraft,
            [Input("Risky", 80, 75, 82, 70, OpportunityDecisionGroup.Risky)],
            AllowRiskyAiDraft: true));

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(OpportunityUserStatus.DraftGenerated, result.Items[0].NewStatus);
    }

    [Fact]
    public void MoveToTestListUpdatesAllItems()
    {
        var service = new OpportunityBatchActionService();

        var result = service.Apply(new OpportunityBatchActionRequest(
            OpportunityBatchActionType.MoveToTestList,
            [
                Input("Strong", 84, 75, 10, 80, OpportunityDecisionGroup.StrongOpportunity),
                Input("Medium", 60, 70, 15, 60, OpportunityDecisionGroup.WorthTesting),
            ],
            AllowRiskyAiDraft: false));

        Assert.Equal(2, result.SuccessCount);
        Assert.All(result.Items, item => Assert.Equal(OpportunityUserStatus.TestList, item.NewStatus));
    }

    [Fact]
    public void RejectMarksItemAsRejected()
    {
        var service = new OpportunityBatchActionService();

        var result = service.Apply(new OpportunityBatchActionRequest(
            OpportunityBatchActionType.Reject,
            [Input("Weak", 25, 20, 5, 30, OpportunityDecisionGroup.Weak)],
            AllowRiskyAiDraft: false));

        Assert.Equal(OpportunityUserStatus.Rejected, result.Items[0].NewStatus);
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
