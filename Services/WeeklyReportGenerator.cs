namespace SimilarProductsWinForms.Services;

using System.Text;
using SimilarProductsWinForms.Models;

internal static class WeeklyReportGenerator
{
    public static WeeklyReport Generate(IEnumerable<ProductCandidate> products)
    {
        var list = products.ToList();
        var title = $"Haftalik Urun Arastirma Raporu - {DateTime.Now:yyyy-MM-dd}";
        var topProducts = BuildTopProducts(list);
        var statusBreakdown = BuildStatusBreakdown(list);
        var categoryBreakdown = BuildCategoryBreakdown(list);
        var actionPlan = BuildActionPlan(list);
        var apiStatus = "Etsy API icin Kisisel Erisim onaylandi. API Ayar ekraninda yeni keystring/shared secret kaydedildikten sonra API Test ve API Ara kullanilabilir. Manuel Veri modu yedek olarak kalir.";

        var summary =
            $"Toplam fikir: {list.Count}{Environment.NewLine}" +
            $"Yuksek oncelikli fikir: {list.Count(product => product.Priority >= 85)}{Environment.NewLine}" +
            $"Not yazilan fikir: {list.Count(product => !string.IsNullOrWhiteSpace(product.Note))}{Environment.NewLine}" +
            $"Listelendi durumundaki fikir: {list.Count(product => product.StatusDisplay == "Listelendi")}";

        var markdown = BuildMarkdown(title, summary, topProducts, statusBreakdown, categoryBreakdown, actionPlan, apiStatus);
        return new WeeklyReport(title, summary, topProducts, statusBreakdown, categoryBreakdown, actionPlan, apiStatus, markdown);
    }

    private static string BuildTopProducts(List<ProductCandidate> products)
    {
        var builder = new StringBuilder();
        foreach (var product in products.OrderByDescending(product => product.Priority).Take(10))
        {
            builder.AppendLine($"- {product.Name} | Puan: {product.Priority} | Durum: {product.StatusDisplay} | Fiyat: {product.CompetitorPriceDisplay}");
        }

        return builder.Length == 0 ? "- Urun yok" : builder.ToString();
    }

    private static string BuildStatusBreakdown(List<ProductCandidate> products)
    {
        var builder = new StringBuilder();
        foreach (var group in products.GroupBy(product => product.StatusDisplay).OrderByDescending(group => group.Count()))
        {
            builder.AppendLine($"- {group.Key}: {group.Count()}");
        }

        return builder.Length == 0 ? "- Durum yok" : builder.ToString();
    }

    private static string BuildCategoryBreakdown(List<ProductCandidate> products)
    {
        var builder = new StringBuilder();
        foreach (var group in products.GroupBy(product => product.Category).OrderByDescending(group => group.Count()).Take(12))
        {
            builder.AppendLine($"- {group.Key}: {group.Count()} fikir");
        }

        return builder.Length == 0 ? "- Kategori yok" : builder.ToString();
    }

    private static string BuildActionPlan(List<ProductCandidate> products)
    {
        var top = products.OrderByDescending(product => product.Priority).Take(5).ToList();
        var builder = new StringBuilder();
        builder.AppendLine("1. En yuksek puanli 3 urun icin Firsat Puani formunu kontrol et.");
        builder.AppendLine("2. Kar Hesapla formunda minimum fiyat ve hedef fiyatlari dogrula.");
        builder.AppendLine("3. SEO Kontrol formunda baslik, aciklama ve tagleri duzelt.");
        builder.AppendLine("4. Listing Taslak formundan fotograf listesi ve paket notlarini hazirla.");
        builder.AppendLine("5. API Ayar ekraninda baglantiyi test et; sonra API Ara ile klon magaza/fiyat bilgisini guncelle.");

        if (top.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Bu hafta odaklanilacak adaylar:");
            foreach (var product in top)
            {
                builder.AppendLine($"- {product.Name}");
            }
        }

        return builder.ToString();
    }

    private static string BuildMarkdown(
        string title,
        string summary,
        string topProducts,
        string statusBreakdown,
        string categoryBreakdown,
        string actionPlan,
        string apiStatus)
    {
        return
            $"# {title}{Environment.NewLine}{Environment.NewLine}" +
            $"## Ozet{Environment.NewLine}{summary}{Environment.NewLine}{Environment.NewLine}" +
            $"## En Yuksek Oncelikli Urunler{Environment.NewLine}{topProducts}{Environment.NewLine}" +
            $"## Durum Dagilimi{Environment.NewLine}{statusBreakdown}{Environment.NewLine}" +
            $"## Kategori Dagilimi{Environment.NewLine}{categoryBreakdown}{Environment.NewLine}" +
            $"## Bu Haftanin Aksiyon Plani{Environment.NewLine}{actionPlan}{Environment.NewLine}" +
            $"## API Durumu{Environment.NewLine}{apiStatus}{Environment.NewLine}";
    }
}
