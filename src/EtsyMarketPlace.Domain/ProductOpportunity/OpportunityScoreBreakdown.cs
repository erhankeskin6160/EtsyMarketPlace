namespace EtsyMarketPlace.Domain.ProductOpportunity;

public sealed record OpportunityScoreBreakdown(
    int Demand,
    int Competition,
    int CompetitionAdvantage,
    int SeoGap,
    int PricePotential,
    int Risk,
    int RiskPenalty,
    decimal DemandWeight,
    decimal CompetitionWeight,
    decimal SeoGapWeight,
    decimal PriceWeight,
    decimal RiskWeight,
    string AlgorithmVersion,
    IReadOnlyList<string> Summary)
{
    public int PositiveSignal => Math.Clamp(Demand + CompetitionAdvantage + SeoGap + PricePotential, 0, 400);
}
