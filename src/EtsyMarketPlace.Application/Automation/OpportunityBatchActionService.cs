namespace EtsyMarketPlace.Application.Automation;

using EtsyMarketPlace.Domain.ProductOpportunity;

public sealed class OpportunityBatchActionService
{
    public OpportunityBatchActionResult Apply(OpportunityBatchActionRequest request)
    {
        var results = request.Items
            .Select(item => ApplyOne(request.Action, item, request.AllowRiskyAiDraft))
            .ToList();

        return new OpportunityBatchActionResult(
            request.Action,
            request.Items.Count,
            results.Count(result => result.Success),
            results.Count(result => !result.Success),
            results);
    }

    private static OpportunityBatchActionItemResult ApplyOne(
        OpportunityBatchActionType action,
        OpportunityAutomationInput item,
        bool allowRiskyAiDraft)
    {
        if (action == OpportunityBatchActionType.GenerateAiDraft && IsRisky(item) && !allowRiskyAiDraft)
        {
            return new OpportunityBatchActionItemResult(
                item.Title,
                false,
                item.UserStatus,
                "Riskli urun AI taslak kuyruguna alinmadi. Manuel onay gerekir.");
        }

        var newStatus = action switch
        {
            OpportunityBatchActionType.MoveToTestList => OpportunityUserStatus.TestList,
            OpportunityBatchActionType.GenerateAiDraft => OpportunityUserStatus.DraftGenerated,
            OpportunityBatchActionType.MarkManualReview => OpportunityUserStatus.Watch,
            OpportunityBatchActionType.Reject => OpportunityUserStatus.Rejected,
            _ => item.UserStatus,
        };

        return new OpportunityBatchActionItemResult(
            item.Title,
            true,
            newStatus,
            MessageFor(action, newStatus));
    }

    private static bool IsRisky(OpportunityAutomationInput item) =>
        item.RiskScore >= 70 || item.DecisionGroup == OpportunityDecisionGroup.Risky;

    private static string MessageFor(OpportunityBatchActionType action, OpportunityUserStatus newStatus) => action switch
    {
        OpportunityBatchActionType.MoveToTestList => "Test listesine alindi.",
        OpportunityBatchActionType.GenerateAiDraft => "AI taslak kuyruguna alindi.",
        OpportunityBatchActionType.MarkManualReview => "Manuel inceleme icin izleme kuyruguna alindi.",
        OpportunityBatchActionType.Reject => "Reddedildi.",
        _ => $"Durum guncellendi: {newStatus}",
    };
}
