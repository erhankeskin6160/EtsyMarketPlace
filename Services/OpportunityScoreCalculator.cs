namespace SimilarProductsWinForms.Services;

using SimilarProductsWinForms.Models;

internal static class OpportunityScoreCalculator
{
    public static OpportunityScoreResult Calculate(
        int demandPotential,
        int shopFit,
        int productionEase,
        int profitPotential,
        int visualAppeal,
        int competitionRisk,
        int shippingRisk,
        int ipRisk)
    {
        var positive =
            demandPotential * 0.25m +
            shopFit * 0.20m +
            productionEase * 0.15m +
            profitPotential * 0.25m +
            visualAppeal * 0.15m;

        var risk =
            competitionRisk * 0.35m +
            shippingRisk * 0.25m +
            ipRisk * 0.40m;

        var score = (int)Math.Round(Math.Clamp(positive - risk * 0.55m, 0m, 100m));
        var strengths = new List<string>();
        var risks = new List<string>();
        var nextActions = new List<string>();

        AddStrength(strengths, demandPotential, "Talep potansiyeli iyi.");
        AddStrength(strengths, shopFit, "Magaza urun cizgisine uyumlu.");
        AddStrength(strengths, productionEase, "Uretim/modelleme kolayligi iyi.");
        AddStrength(strengths, profitPotential, "Kar potansiyeli yuksek.");
        AddStrength(strengths, visualAppeal, "Gorsel vitrin etkisi guclu.");

        AddRisk(risks, competitionRisk, "Rekabet riski yuksek; farklilastirma gerekli.");
        AddRisk(risks, shippingRisk, "Kargo/hasar riski yuksek; paketleme planini kontrol edin.");
        AddRisk(risks, ipRisk, "Marka/IP riski yuksek; resmi urun izlenimi vermeyen dil kullanin.");

        var decision = score switch
        {
            >= 78 => "Oncelikli uretim adayi",
            >= 62 => "Test baski ve fotograf denemesi yap",
            >= 45 => "Beklet, veri veya farklilastirma lazim",
            _ => "Simdilik dusuk oncelik",
        };

        if (score >= 78)
        {
            nextActions.Add("Urunu test baskiya alin ve 3 ana fotograf acisini planlayin.");
            nextActions.Add("Kar hesaplayicida minimum fiyati dogrulayin.");
        }
        else if (score >= 62)
        {
            nextActions.Add("Kucuk bir varyasyon veya aksesuar olarak test edin.");
            nextActions.Add("SEO kontrol formunda baslik ve 13 tag taslagini iyilestirin.");
        }
        else
        {
            nextActions.Add("Rakip fiyat ve talep verisi gelene kadar bekletin.");
            nextActions.Add("Uretim zorlugu veya IP riskini dusurecek alternatif tasarim dusunun.");
        }

        return new OpportunityScoreResult(score, decision, positive, risk, strengths, risks, nextActions);
    }

    private static void AddStrength(List<string> strengths, int value, string message)
    {
        if (value >= 70)
        {
            strengths.Add(message);
        }
    }

    private static void AddRisk(List<string> risks, int value, string message)
    {
        if (value >= 65)
        {
            risks.Add(message);
        }
    }
}
