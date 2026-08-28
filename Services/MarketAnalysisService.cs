namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Models;

internal sealed class MarketAnalysisService
{
    private static readonly HttpClient HttpClient = new();

    public sealed class MarketSummaryKpis
    {
        public int TotalListings { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public string Currency { get; set; } = "USD";
        public double AverageFavorites { get; set; }
        public double AverageViews { get; set; }
        public string TopShopName { get; set; } = "-";
        public int TopShopSales { get; set; }
        public int OpportunityScore { get; set; } // 0-100
        public List<KeyValuePair<string, int>> TopTags { get; set; } = [];
    }

    /// <summary>
    /// Arama sonuçlarındaki listelemeleri analiz ederek pazarın anlık KPI metriklerini hesaplar.
    /// </summary>
    public static MarketSummaryKpis CalculateKpis(IReadOnlyList<MarketListingResult> listings)
    {
        var kpis = new MarketSummaryKpis();
        if (listings == null || listings.Count == 0)
        {
            return kpis;
        }

        kpis.TotalListings = listings.Count;

        // Fiyat Hesaplama
        var validPrices = new List<decimal>();
        foreach (var l in listings)
        {
            if (decimal.TryParse(l.PriceDisplay?.Replace("$", "").Replace("€", "").Replace("₺", "").Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out decimal p) && p > 0)
            {
                validPrices.Add(p);
            }
        }

        if (validPrices.Count > 0)
        {
            kpis.AveragePrice = Math.Round(validPrices.Average(), 2);
            kpis.MinPrice = validPrices.Min();
            kpis.MaxPrice = validPrices.Max();
        }

        // Favori ve Görüntülenme
        var favs = listings.Select(x => (double)x.Favorites).ToList();
        kpis.AverageFavorites = favs.Count > 0 ? Math.Round(favs.Average(), 1) : 0;

        var views = listings.Select(x => (double)x.Views).ToList();
        kpis.AverageViews = views.Count > 0 ? Math.Round(views.Average(), 1) : 0;

        // En Çok Satan Mağaza
        var topShop = listings
            .Where(x => !string.IsNullOrWhiteSpace(x.ShopName))
            .OrderByDescending(x => x.ShopSales)
            .FirstOrDefault();

        if (topShop != null)
        {
            kpis.TopShopName = topShop.ShopName;
            kpis.TopShopSales = topShop.ShopSales;
        }

        // Fırsat Skoru Hesaplama (0-100):
        // Yüksek ortalama favori/talep + dengeli fiyat = Yüksek fırsat
        double demandFactor = Math.Min(100.0, kpis.AverageFavorites * 1.5);
        double priceFactor = kpis.AveragePrice >= 15 && kpis.AveragePrice <= 75 ? 90 : 65;
        double competitionFactor = listings.Count >= 30 ? 70 : 85;
        kpis.OpportunityScore = (int)Math.Clamp((demandFactor * 0.45) + (priceFactor * 0.35) + (competitionFactor * 0.20), 20, 98);

        // Tag Analizi (Frekans)
        var tagFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in listings)
        {
            if (l.Tags != null)
            {
                foreach (var tag in l.Tags)
                {
                    string cleaned = tag?.Trim() ?? "";
                    if (cleaned.Length >= 2)
                    {
                        tagFreq[cleaned] = tagFreq.TryGetValue(cleaned, out int count) ? count + 1 : 1;
                    }
                }
            }
        }

        kpis.TopTags = tagFreq.OrderByDescending(x => x.Value).Take(13).ToList();
        return kpis;
    }

    /// <summary>
    /// ChatGPT veya Gemini kullanarak pazar hakkında stratejik e-ticaret ve ürün fırsat raporu üretir.
    /// </summary>
    public static async Task<string> GenerateAiMarketReportAsync(
        string keyword,
        MarketSummaryKpis kpis,
        IReadOnlyList<MarketListingResult> sampleListings,
        AiOptimizationSettings aiSettings,
        CancellationToken cancellationToken = default)
    {
        string prompt = BuildMarketPrompt(keyword, kpis, sampleListings);

        if (aiSettings.UseOpenAi)
        {
            try
            {
                var res = await FetchOpenAiMarketReportAsync(prompt, aiSettings.OpenAiApiKey, aiSettings.OpenAiModel, cancellationToken);
                if (!string.IsNullOrWhiteSpace(res)) return res;
            }
            catch { }
        }
        else if (aiSettings.UseGemini)
        {
            try
            {
                var res = await FetchGeminiMarketReportAsync(prompt, aiSettings.GeminiApiKey, aiSettings.GeminiModel, cancellationToken);
                if (!string.IsNullOrWhiteSpace(res)) return res;
            }
            catch { }
        }

        // Fallback: Yerel Kural Tabanlı Pazar Raporu
        return GenerateDeterministicReport(keyword, kpis);
    }

    private static string BuildMarketPrompt(string keyword, MarketSummaryKpis kpis, IReadOnlyList<MarketListingResult> sampleListings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen kıdemli bir Etsy E-Ticaret Danışmanı ve 3D Baskı / El Emeği Pazar Stratejistisin.");
        sb.AppendLine($"Aşağıda '{keyword}' anahtar kelimesi için yapılan pazar araştırmasının gerçek verileri bulunmaktadır:");
        sb.AppendLine($"- Toplam İncelenen Ürün: {kpis.TotalListings}");
        sb.AppendLine($"- Ortalama Satış Fiyatı: ${kpis.AveragePrice} (Min: ${kpis.MinPrice}, Max: ${kpis.MaxPrice})");
        sb.AppendLine($"- Ortalama Favori Sayısı: {kpis.AverageFavorites:F1}");
        sb.AppendLine($"- Lider Rakip Mağaza: {kpis.TopShopName} ({kpis.TopShopSales:N0} satış)");
        sb.AppendLine($"- En Çok Kullanılan Tagler: {string.Join(", ", kpis.TopTags.Select(t => t.Key))}");

        if (sampleListings != null && sampleListings.Count > 0)
        {
            sb.AppendLine("\nÖrnek İlk 5 Rakip Başlığı:");
            foreach (var l in sampleListings.Take(5))
            {
                sb.AppendLine($"* {l.Title} | Fiyat: {l.PriceDisplay} | Favori: {l.Favorites}");
            }
        }

        sb.AppendLine("\nLütfen Etsy satıcısına şu başlıklar altında doğrudan uygulanabilir, profesyonel ve motive edici bir Pazar Strateji Raporu hazırla (Türkçe):");
        sb.AppendLine("1. 🎯 Pazar Fırsatı & Rekabet Özeti (Bu nişe girmeye değer mi?)");
        sb.AppendLine("2. 💡 Önerilen İdeal Fiyat & Paket Stratejisi (Hangi fiyattan listelemeliyiz?)");
        sb.AppendLine("3. 🚀 3D Baskı / El Emeği için Fark Yaratan Ürün Fikirleri (Rakiplerde olmayan ne yapılabilir?)");
        sb.AppendLine("4. 🔑 Önerilen Kazanan 5 Long-Tail Anahtar Kelime");
        sb.AppendLine("5. ⚡ 3 Adımda Hızlı Satış Eylem Planı");
        return sb.ToString();
    }

    private static async Task<string> FetchOpenAiMarketReportAsync(string prompt, string apiKey, string model, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        string actualModel = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model.Trim();
        var payload = new
        {
            model = actualModel,
            messages = new[]
            {
                new { role = "system", content = "Sen Etsy e-ticaret pazar analisti ve stratejistisin. Yanıtlarını net, maddeler halinde ve Türkçe ver." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return string.Empty;

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private static async Task<string> FetchGeminiMarketReportAsync(string prompt, string apiKey, string model, CancellationToken cancellationToken)
    {
        string actualModel = string.IsNullOrWhiteSpace(model) ? "gemini-1.5-flash" : model.Trim();
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{actualModel}:generateContent?key={apiKey.Trim()}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return string.Empty;

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private static string GenerateDeterministicReport(string keyword, MarketSummaryKpis kpis)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📊 '{keyword}' PAZAR STRATEJİ RAPORU");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"\n🎯 1. Pazar Özeti & Fırsat Skoru: %{kpis.OpportunityScore}/100");
        sb.AppendLine($"* Ortalama Pazar Fiyatı: ${kpis.AveragePrice} (Giriş seviyesi: ${kpis.MinPrice}, Premium: ${kpis.MaxPrice})");
        sb.AppendLine($"* Talep Düzeyi: Ortalama {kpis.AverageFavorites:F1} favori ile aktif alıcı ilgisi mevcut.");
        sb.AppendLine($"* Pazar Lideri: '{kpis.TopShopName}' ({kpis.TopShopSales:N0} satış)");

        sb.AppendLine("\n💡 2. Fiyatlandırma Tavsiyesi:");
        decimal targetPrice = Math.Round(kpis.AveragePrice * 0.95m, 2);
        sb.AppendLine($"* Rekabetçi Lansman Fiyatı: ${targetPrice} civarı önerilir.");
        sb.AppendLine($"* Ücretsiz Kargo (Free Shipping) eşiği dahil edilerek ${targetPrice + 4.99m:F2} olarak listelenebilir.");

        sb.AppendLine("\n🏷️ 3. Rakiplerin En Çok Tutan 13 Tag'i:");
        sb.AppendLine(string.Join(", ", kpis.TopTags.Select(t => t.Key)));

        sb.AppendLine("\n🚀 4. Hızlı Satış Eylem Planı:");
        sb.AppendLine("1. '1-Tıkla Taslağa Klonla' butonuyla rakibin yapısını taslak olarak içeri aktarın.");
        sb.AppendLine("2. 'AI Optimizasyon' ile başlığı ve 13 etiketi kendi ürününüze göre zenginleştirin.");
        sb.AppendLine("3. 'AI Görsel Studio' modülünden 2000x2000 HD mockup görseli hazırlayıp kapak resmi yapın.");
        return sb.ToString();
    }
}
