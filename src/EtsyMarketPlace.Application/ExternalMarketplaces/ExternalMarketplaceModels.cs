namespace EtsyMarketPlace.Application.ExternalMarketplaces;

using EtsyMarketPlace.Domain.ProductOpportunity;

public sealed record ExternalMarketplaceSearchRequest(
    string ShopType,
    string Keyword,
    IReadOnlyCollection<string> EnabledSources);

public sealed record ExternalMarketplaceSearchContext(
    string ShopType,
    string Keyword,
    string Query);

public sealed record MarketplaceProduct(
    string Source,
    string Title,
    string ProductUrl,
    string SearchUrl,
    string SellerName,
    string ImageUrl,
    string CategoryHint,
    IReadOnlyList<string> Tags,
    decimal Price,
    int BaseScore,
    int DemandSignal,
    int ShopSignal,
    string Notes);

public sealed record ExternalMarketplaceOpportunity(
    MarketplaceProduct Product,
    string Category,
    IReadOnlyList<string> Tags,
    int OpportunityScore,
    int EtsyFitScore,
    int RiskScore,
    OpportunityScoreBreakdown ScoreBreakdown,
    OpportunityDecisionGroup DecisionGroup,
    OpportunityUserStatus SuggestedStatus,
    string Decision,
    IReadOnlyList<string> RiskTerms,
    IReadOnlyList<string> Reasons);
