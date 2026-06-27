namespace EtsyMarketPlace.Application.ProductOpportunity;

public sealed record ProductOpportunityInput(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    decimal Price,
    int Favorites,
    int Views,
    int ShopSales,
    int SeoScore,
    int Quantity,
    string TargetKeyword,
    string Category);

public sealed record ProductOpportunityScore(
    int Opportunity,
    int Demand,
    int Competition,
    int SeoGap,
    int PricePotential,
    int Risk,
    string Decision,
    IReadOnlyList<string> Reasons);
