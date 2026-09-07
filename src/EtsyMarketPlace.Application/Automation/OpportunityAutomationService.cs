namespace EtsyMarketPlace.Application.Automation;

using EtsyMarketPlace.Domain.ProductOpportunity;

public sealed class OpportunityAutomationService
{
    public OpportunityAutomationReport BuildReport(IEnumerable<OpportunityAutomationInput> inputs)
    {
        var items = inputs
            .Select(input => new OpportunityAutomationItem(
                input.Title,
                input.OpportunityScore,
                input.DemandScore,
                input.RiskScore,
                input.EtsyFitScore,
                input.DecisionGroup,
                input.UserStatus,
                Recommend(input)))
            .OrderBy(item => PriorityRank(item.Recommendation.Priority))
            .ThenByDescending(item => item.OpportunityScore)
            .ToList();

        var summary = new OpportunityAutomationSummary(
            items.Count,
            items.Count(item => item.DecisionGroup == OpportunityDecisionGroup.StrongOpportunity),
            items.Count(item => item.DecisionGroup == OpportunityDecisionGroup.WorthTesting),
            items.Count(item => item.DecisionGroup == OpportunityDecisionGroup.Risky),
            items.Count(item => item.DecisionGroup == OpportunityDecisionGroup.Weak),
            items.Count(item => item.Recommendation.Action == "AI taslak uret"),
            items.Count(item => item.Recommendation.Action == "Manuel kontrol"),
            items.Count == 0 ? 0 : Math.Round((decimal)items.Average(item => item.OpportunityScore), 1));

        return new OpportunityAutomationReport(summary, items);
    }

    public OpportunityAutomationRecommendation Recommend(OpportunityAutomationInput input)
    {
        if (input.RiskScore >= 70 || input.DecisionGroup == OpportunityDecisionGroup.Risky)
        {
            return new OpportunityAutomationRecommendation(
                "Manuel kontrol",
                "Yuksek",
                "Risk skoru yuksek. Marka, telif, kategori ve Etsy politika riski kontrol edilmeden taslak uretilmemeli.");
        }

        if (input.UserStatus == OpportunityUserStatus.AddedToEtsyDraft)
        {
            return new OpportunityAutomationRecommendation(
                "Takibe al",
                "Dusuk",
                "Urun Etsy taslak kuyruguna eklenmis. Sonraki adim performans ve yayin kontrolu.");
        }

        if (input.OpportunityScore >= 75 && input.EtsyFitScore >= 65)
        {
            return new OpportunityAutomationRecommendation(
                "AI taslak uret",
                "Yuksek",
                "Firsat ve Etsy uyum skoru guclu. AI baslik, aciklama, tag ve gorsel taslagi uretilebilir.");
        }

        if (input.OpportunityScore >= 55 || input.DemandScore >= 65)
        {
            return new OpportunityAutomationRecommendation(
                "Test listesine al",
                "Orta",
                "Talep veya firsat sinyali var. Kucuk adetli test listingi icin izlenebilir.");
        }

        return new OpportunityAutomationRecommendation(
            "Beklet / ele",
            "Dusuk",
            "Sinyal zayif. Daha iyi kaynak urun, daha net kategori veya daha iyi fiyat dogrulamasi beklenmeli.");
    }

    private static int PriorityRank(string priority) => priority switch
    {
        "Yuksek" => 0,
        "Orta" => 1,
        _ => 2,
    };
}
