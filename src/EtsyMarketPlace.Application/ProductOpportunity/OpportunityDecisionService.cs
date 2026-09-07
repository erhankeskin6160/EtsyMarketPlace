namespace EtsyMarketPlace.Application.ProductOpportunity;

using EtsyMarketPlace.Domain.ProductOpportunity;

public sealed class OpportunityDecisionService
{
    public OpportunityDecisionResult Classify(ProductOpportunityScore score)
    {
        if (score.Risk >= 70)
        {
            return new OpportunityDecisionResult(
                OpportunityDecisionGroup.Risky,
                OpportunityUserStatus.Watch,
                "Riskli",
                "Marka, telif veya politika riski yuksek oldugu icin once manuel kontrol gerekir.");
        }

        if (score.Opportunity >= 75)
        {
            return new OpportunityDecisionResult(
                OpportunityDecisionGroup.StrongOpportunity,
                OpportunityUserStatus.TestList,
                "Guclu firsat",
                "Talep, fiyat ve rekabet sinyalleri test listingi icin yeterince guclu.");
        }

        if (score.Opportunity >= 55)
        {
            return new OpportunityDecisionResult(
                OpportunityDecisionGroup.WorthTesting,
                OpportunityUserStatus.Watch,
                "Test edilebilir",
                "Potansiyel var; kucuk adetli taslak ve daha iyi SEO ile denenebilir.");
        }

        return new OpportunityDecisionResult(
            OpportunityDecisionGroup.Weak,
            OpportunityUserStatus.New,
            "Zayif / beklet",
            "Sinyaller zayif; daha iyi rakip, kategori veya fiyat dogrulamasi gerekir.");
    }

    public static bool MatchesFilter(OpportunityDecisionGroup filter, OpportunityDecisionGroup group) =>
        filter == OpportunityDecisionGroup.All || filter == group;
}
