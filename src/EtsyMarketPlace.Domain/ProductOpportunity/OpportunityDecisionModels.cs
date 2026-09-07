namespace EtsyMarketPlace.Domain.ProductOpportunity;

public enum OpportunityDecisionGroup
{
    All = 0,
    StrongOpportunity = 1,
    WorthTesting = 2,
    Risky = 3,
    Weak = 4,
}

public enum OpportunityUserStatus
{
    New = 0,
    Watch = 1,
    TestList = 2,
    DraftGenerated = 3,
    AddedToEtsyDraft = 4,
    Rejected = 5,
}

public sealed record OpportunityDecisionResult(
    OpportunityDecisionGroup Group,
    OpportunityUserStatus DefaultStatus,
    string Label,
    string Reason);
