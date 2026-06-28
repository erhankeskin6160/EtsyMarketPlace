namespace EtsyMarketPlace.Application.Automation;

using EtsyMarketPlace.Domain.ProductOpportunity;

public sealed record OpportunityAutomationInput(
    string Title,
    int OpportunityScore,
    int DemandScore,
    int RiskScore,
    int EtsyFitScore,
    OpportunityDecisionGroup DecisionGroup,
    OpportunityUserStatus UserStatus);

public sealed record OpportunityAutomationRecommendation(
    string Action,
    string Priority,
    string Reason);

public sealed record OpportunityAutomationItem(
    string Title,
    int OpportunityScore,
    int DemandScore,
    int RiskScore,
    int EtsyFitScore,
    OpportunityDecisionGroup DecisionGroup,
    OpportunityUserStatus UserStatus,
    OpportunityAutomationRecommendation Recommendation);

public sealed record OpportunityAutomationSummary(
    int Total,
    int Strong,
    int WorthTesting,
    int Risky,
    int Weak,
    int DraftReady,
    int ManualReview,
    decimal AverageOpportunity);

public sealed record OpportunityAutomationReport(
    OpportunityAutomationSummary Summary,
    IReadOnlyList<OpportunityAutomationItem> Items);
