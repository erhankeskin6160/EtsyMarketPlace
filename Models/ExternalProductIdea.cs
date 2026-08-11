namespace SimilarProductsWinForms.Models;

internal sealed class ExternalProductIdea
{
    public bool IsSelected { get; set; }
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
    public int DemandScore { get; init; }
    public int CompetitionAdvantageScore { get; init; }
    public int SeoGapScore { get; init; }
    public int PricePotentialScore { get; init; }
    public string DecisionGroup { get; init; } = "";
    public string SuggestedStatus { get; init; } = "";
    public string UserStatus { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public string ActionPriority { get; set; } = "";
    public string ActionReason { get; set; } = "";
    public string Decision { get; init; } = "";
    public string Reasons { get; init; } = "";
    public string RiskTerms { get; init; } = "";
    public string ScoreDetails { get; init; } = "";
    public string Opportunity => $"{OpportunityScore}/100";
    public string EtsyFit => $"{EtsyFitScore}/100";
    public string Risk => $"{RiskScore}/100";
    public string Demand => $"{DemandScore}/100";
    public string CompetitionAdvantage => $"{CompetitionAdvantageScore}/100";
    public string SeoGap => $"{SeoGapScore}/100";
    public string PricePotential => $"{PricePotentialScore}/100";
}
