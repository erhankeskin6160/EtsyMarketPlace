namespace SimilarProductsWinForms.Models;

internal sealed record OpportunityScoreResult(
    int Score,
    string Decision,
    decimal WeightedPositiveScore,
    decimal WeightedRiskScore,
    List<string> Strengths,
    List<string> Risks,
    List<string> NextActions);
