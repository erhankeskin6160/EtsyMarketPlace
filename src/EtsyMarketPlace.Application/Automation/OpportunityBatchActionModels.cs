namespace EtsyMarketPlace.Application.Automation;

using EtsyMarketPlace.Domain.ProductOpportunity;

public enum OpportunityBatchActionType
{
    MoveToTestList = 0,
    GenerateAiDraft = 1,
    MarkManualReview = 2,
    Reject = 3,
}

public sealed record OpportunityBatchActionRequest(
    OpportunityBatchActionType Action,
    IReadOnlyList<OpportunityAutomationInput> Items,
    bool AllowRiskyAiDraft);

public sealed record OpportunityBatchActionItemResult(
    string Title,
    bool Success,
    OpportunityUserStatus NewStatus,
    string Message);

public sealed record OpportunityBatchActionResult(
    OpportunityBatchActionType Action,
    int RequestedCount,
    int SuccessCount,
    int BlockedCount,
    IReadOnlyList<OpportunityBatchActionItemResult> Items);
