namespace SimilarProductsWinForms.Models;

internal sealed class ExternalProductIdea
{
    public string Source { get; init; } = "";
    public string Title { get; init; } = "";
    public string SearchUrl { get; init; } = "";
    public string ProductUrl { get; set; } = "";
    public string Price { get; set; } = "";
    public string SellerName { get; init; } = "";
    public string ImageUrl { get; init; } = "";
    public string Category { get; init; } = "";
    public string Tags { get; init; } = "";
    public string Notes { get; init; } = "";
    public int OpportunityScore { get; init; }
    public int EtsyFitScore { get; init; }
    public int RiskScore { get; init; }
    public string Decision { get; init; } = "";
    public string Reasons { get; init; } = "";
    public string RiskTerms { get; init; } = "";
    public string Opportunity => $"{OpportunityScore}/100";
    public string EtsyFit => $"{EtsyFitScore}/100";
    public string Risk => $"{RiskScore}/100";
}
