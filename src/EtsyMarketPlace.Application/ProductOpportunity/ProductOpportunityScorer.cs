namespace EtsyMarketPlace.Application.ProductOpportunity;

using EtsyMarketPlace.Domain.ProductOpportunity;

public sealed class ProductOpportunityScorer
{
    private const string AlgorithmVersion = "opportunity-v2.1";
    private const decimal DemandWeight = 0.34m;
    private const decimal CompetitionWeight = 0.20m;
    private const decimal SeoGapWeight = 0.18m;
    private const decimal PriceWeight = 0.18m;
    private const decimal RiskWeight = 0.20m;

    private static readonly string[] RiskTerms =
    [
        "disney", "marvel", "dc", "pokemon", "nintendo", "harry potter", "star wars", "lotr",
        "lord of the rings", "ben 10", "omnitrix", "minecraft", "valorant", "spiderman",
        "spider-man", "batman", "superman", "captain america", "iron man", "hulk", "naruto",
        "demon slayer", "god of war", "gandalf", "sauron", "aragorn", "jack sparrow",
    ];

    public ProductOpportunityScore Score(ProductOpportunityInput input)
    {
        var demand = DemandScore(input);
        var competition = CompetitionScore(input);
        var competitionAdvantage = Math.Clamp(100 - competition, 0, 100);
        var seoGap = Math.Clamp(100 - input.SeoScore, 0, 100);
        var pricePotential = PricePotentialScore(input.Price);
        var risk = RiskScore(input);
        var riskPenalty = Math.Clamp(risk, 0, 100);

        var opportunity = (int)Math.Round(
            demand * DemandWeight +
            seoGap * SeoGapWeight +
            pricePotential * PriceWeight +
            competitionAdvantage * CompetitionWeight -
            riskPenalty * RiskWeight);
        opportunity = Math.Clamp(opportunity, 0, 100);
        var breakdown = new OpportunityScoreBreakdown(
            demand,
            competition,
            competitionAdvantage,
            seoGap,
            pricePotential,
            risk,
            riskPenalty,
            DemandWeight,
            CompetitionWeight,
            SeoGapWeight,
            PriceWeight,
            RiskWeight,
            AlgorithmVersion,
            BreakdownSummary(demand, competitionAdvantage, seoGap, pricePotential, risk));

        return new ProductOpportunityScore(
            opportunity,
            demand,
            competition,
            seoGap,
            pricePotential,
            risk,
            Decision(opportunity, risk),
            breakdown,
            Reasons(input, demand, competition, seoGap, pricePotential, risk));
    }

    public IReadOnlyList<string> DetectRiskTerms(ProductOpportunityInput input)
    {
        var blob = $"{input.Title} {input.Description} {string.Join(' ', input.Tags)} {input.TargetKeyword} {input.Category}";
        return RiskTerms
            .Where(term => blob.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int DemandScore(ProductOpportunityInput input)
    {
        var favoriteScore = Scale(input.Favorites, 0, 250);
        var viewScore = Scale(input.Views, 0, 5000);
        var shopScore = Scale(input.ShopSales, 0, 5000);
        return Weighted(favoriteScore, 0.42m, viewScore, 0.34m, shopScore, 0.24m);
    }

    private static int CompetitionScore(ProductOpportunityInput input)
    {
        var shopPower = Scale(input.ShopSales, 0, 15000);
        var listingHeat = Scale(input.Favorites + input.Views / 20, 0, 650);
        var seoStrength = Math.Clamp(input.SeoScore, 0, 100);
        return Weighted(shopPower, 0.42m, listingHeat, 0.28m, seoStrength, 0.30m);
    }

    private static int PricePotentialScore(decimal price)
    {
        if (price <= 0) return 35;
        if (price < 8) return 25;
        if (price <= 20) return 58;
        if (price <= 120) return 92;
        if (price <= 250) return 76;
        return 48;
    }

    private static int RiskScore(ProductOpportunityInput input)
    {
        var blob = $"{input.Title} {input.Description} {string.Join(' ', input.Tags)} {input.TargetKeyword} {input.Category}";
        var hits = RiskTerms.Count(term => blob.Contains(term, StringComparison.OrdinalIgnoreCase));
        var risk = Math.Min(95, hits * 18);
        if (blob.Contains("official", StringComparison.OrdinalIgnoreCase) ||
            blob.Contains("licensed", StringComparison.OrdinalIgnoreCase) ||
            blob.Contains("replica", StringComparison.OrdinalIgnoreCase))
        {
            risk += 12;
        }

        return Math.Clamp(risk, 0, 100);
    }

    private static IReadOnlyList<string> Reasons(
        ProductOpportunityInput input,
        int demand,
        int competition,
        int seoGap,
        int pricePotential,
        int risk)
    {
        var reasons = new List<string>();
        reasons.Add(demand >= 70
            ? "Talep sinyali guclu: favori, goruntulenme veya magaza satisi yuksek."
            : "Talep sinyali orta/dusuk: urunu kucuk test listingi olarak denemek daha guvenli.");
        reasons.Add(competition >= 70
            ? "Rekabet guclu: rakip magazalar ve listingler kuvvetli gorunuyor."
            : "Rekabet yonetilebilir: daha iyi SEO ve gorselle ayrisma sansi var.");
        reasons.Add(seoGap >= 35
            ? "SEO boslugu var: rakip listing daha iyi baslik/tag/aciklama ile gecilebilir."
            : "SEO boslugu dar: listing kalitesi zaten gorece iyi.");
        reasons.Add(pricePotential >= 75
            ? "Fiyat araligi kar potansiyeli icin uygun."
            : "Fiyat araligi dikkat istiyor; maliyet ve komisyon kontrol edilmeli.");
        if (risk >= 45)
        {
            reasons.Add("Marka/telif riski yuksek olabilir; guvenli jenerik konumlandirma gerekli.");
        }

        if (input.Quantity <= 2)
        {
            reasons.Add("Stok dusuk gorunuyor; rakip uretim kapasitesi sinirli olabilir.");
        }

        return reasons;
    }

    private static IReadOnlyList<string> BreakdownSummary(
        int demand,
        int competitionAdvantage,
        int seoGap,
        int pricePotential,
        int risk)
    {
        var summary = new List<string>
        {
            $"Talep: {demand}/100",
            $"Rekabet avantaji: {competitionAdvantage}/100",
            $"SEO boslugu: {seoGap}/100",
            $"Fiyat potansiyeli: {pricePotential}/100",
            $"Risk cezasi: -{risk}/100",
        };

        if (demand >= 70 && competitionAdvantage >= 45)
        {
            summary.Add("Yuksek talep ve yonetilebilir rekabet birlikte gorunuyor.");
        }

        if (risk >= 45)
        {
            summary.Add("Marka/telif riski manuel kontrol gerektirir.");
        }

        return summary;
    }

    private static string Decision(int opportunity, int risk) =>
        risk >= 70 ? "Riskli, dikkatli incele" :
        opportunity >= 75 ? "Guclu firsat" :
        opportunity >= 55 ? "Test etmeye deger" :
        opportunity >= 40 ? "Izlemeye al" :
        "Zayif firsat";

    private static int Scale(int value, int min, int max)
    {
        if (max <= min) return 0;
        var score = (value - min) * 100m / (max - min);
        return (int)Math.Round(Math.Clamp(score, 0m, 100m));
    }

    private static int Weighted(int first, decimal firstWeight, int second, decimal secondWeight, int third, decimal thirdWeight) =>
        (int)Math.Round(first * firstWeight + second * secondWeight + third * thirdWeight);
}
