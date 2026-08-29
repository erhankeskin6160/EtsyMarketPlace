namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ShopPerformance;
using SimilarProductsWinForms.Models;

internal static class ShopAiConsultantService
{
    private static readonly HttpClient HttpClient = new();

    public static async Task<string> GenerateStoreAuditReportAsync(
        ShopPerformanceComparison comparison,
        ShopParetoAnalysisService.ParetoSummary pareto,
        AiOptimizationSettings aiSettings,
        CancellationToken cancellationToken = default)
    {
        string prompt = BuildPrompt(comparison, pareto);

        if (aiSettings.UseOpenAi)
        {
            try
            {
                var res = await FetchOpenAiReportAsync(prompt, aiSettings.OpenAiApiKey, aiSettings.OpenAiModel, cancellationToken);
                if (!string.IsNullOrWhiteSpace(res)) return res;
            }
            catch { }
        }
        else if (aiSettings.UseGemini)
        {
            try
            {
                var res = await FetchGeminiReportAsync(prompt, aiSettings.GeminiApiKey, aiSettings.GeminiModel, cancellationToken);
                if (!string.IsNullOrWhiteSpace(res)) return res;
            }
            catch { }
        }

        return GenerateDeterministicAudit(comparison, pareto);
    }

    private static string BuildPrompt(ShopPerformanceComparison comparison, ShopParetoAnalysisService.ParetoSummary pareto)
    {
        var cur = comparison.Current;
        var prev = comparison.Previous;

        var sb = new StringBuilder();
        sb.AppendLine("Sen kıdemli bir Etsy E-Ticaret ve Mağaza Büyüme (Growth) Baş Danışmanısın.");
        sb.AppendLine("Aşağıda bir Etsy mağazasının iki dönemlik performans kıyaslama ve ABC Pareto verileri bulunmaktadır:\n");

        sb.AppendLine("📊 DÖNEM PERFORMANSI:");
        sb.AppendLine($"- Seçilen Dönem: {cur.OrderCount} Sipariş | {cur.UnitsSold} Adet | Ciro: ${cur.GrossRevenue:N2} | Ort. Sipariş: ${cur.AverageOrderValue:N2}");
        sb.AppendLine($"- Önceki Dönem: {prev.OrderCount} Sipariş | {prev.UnitsSold} Adet | Ciro: ${prev.GrossRevenue:N2} | Ort. Sipariş: ${prev.AverageOrderValue:N2}");
        sb.AppendLine($"- Ciro Değişimi: %{comparison.Revenue.PercentageChange:F1} | Sipariş Değişimi: %{comparison.Orders.PercentageChange:F1}\n");

        sb.AppendLine("🏆 ABC / PARETO SEGMENTASYONU:");
        sb.AppendLine($"- A Grubu (Yıldız Ürünler - %80 Ciro): {pareto.GroupACount} adet ürün (${pareto.GroupARevenue:N2})");
        sb.AppendLine($"- B Grubu (Potansiyelli Ürünler - %15 Ciro): {pareto.GroupBCount} adet ürün (${pareto.GroupBRevenue:N2})");
        sb.AppendLine($"- C Grubu (Yavaş Satan / Ölü Stok): {pareto.GroupCCount} adet ürün (${pareto.GroupCRevenue:N2})\n");

        sb.AppendLine("Öne Çıkan Ürünler:");
        foreach (var p in comparison.Products.Take(6))
        {
            sb.AppendLine($"* {p.Title} -> Bu dönem Ciro: ${p.CurrentRevenue:N2} (Fark: ${p.RevenueDifference:N2}, Adet: {p.CurrentUnitsSold})");
        }

        sb.AppendLine("\nLütfen mağaza sahibine doğrudan uygulanabilir, profesyonel, yapıcı ve maddeler halinde bir 'Etsy Mağaza Büyüme & Aksiyon Raporu' hazırla (Türkçe):");
        sb.AppendLine("1. 🎯 Genel Mağaza Sağlığı & Dönem Değerlendirmesi (Ciro trendi ne durumda?)");
        sb.AppendLine("2. 🔻 Alarm Veren & Düşüşteki Ürünler (Hangi ürünlerde acil fiyat veya SEO revizyonu gerekiyor?)");
        sb.AppendLine("3. 🚀 Lokomotif Ürünleri Katlama Stratejisi (A grubu yıldızları nasıl daha çok sattırabiliriz?)");
        sb.AppendLine("4. 💡 Sepet Tutarını (AOV) Artırma Taktikleri (Paketleme, Bundle, Varyasyon önerileri)");
        sb.AppendLine("5. ⚡ Önümüzdeki 7 Gün İçin 3 Öncelikli Eylem Maddesi");
        return sb.ToString();
    }

    private static async Task<string> FetchOpenAiReportAsync(string prompt, string apiKey, string model, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        string actualModel = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model.Trim();
        var payload = new
        {
            model = actualModel,
            messages = new[]
            {
                new { role = "system", content = "Sen profesyonel Etsy e-ticaret danışmanısın. Raporlarını net, veriye dayalı ve Türkçe oluştur." },
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

    private static async Task<string> FetchGeminiReportAsync(string prompt, string apiKey, string model, CancellationToken cancellationToken)
    {
        string actualModel = AiModelNormalizer.NormalizeGeminiTextModel(model);
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

    private static string GenerateDeterministicAudit(ShopPerformanceComparison comparison, ShopParetoAnalysisService.ParetoSummary pareto)
    {
        var cur = comparison.Current;
        var prev = comparison.Previous;

        var sb = new StringBuilder();
        sb.AppendLine("📊 ETSY MAĞAZA STRATEJİK BÜYÜME RAPORU (Kural Tabanlı Danışman)");
        sb.AppendLine(new string('=', 60));

        sb.AppendLine($"\n🎯 1. Dönem Değerlendirmesi:");
        sb.AppendLine($"* Bu Dönem Ciro: ${cur.GrossRevenue:N2} ({cur.OrderCount} Sipariş, Ort. Sipariş: ${cur.AverageOrderValue:N2})");
        sb.AppendLine($"* Önceki Dönem: ${prev.GrossRevenue:N2} ({prev.OrderCount} Sipariş)");
        sb.AppendLine($"* Ciro Trendi: %{comparison.Revenue.PercentageChange:+0.0;-0.0;0.0} {(comparison.Revenue.Difference >= 0 ? "🟢 Artışta" : "🔴 Düşüşte")}");

        sb.AppendLine("\n🏆 2. ABC / Pareto Ürün Analizi:");
        sb.AppendLine($"* 🟢 A Grubu (Yıldızlar): {pareto.GroupACount} ürün cironun %80'ini oluşturuyor (${pareto.GroupARevenue:N2}). Bu ürünlerin stoklarını asla bitirmeyin.");
        sb.AppendLine($"* 🟡 B Grubu (Potansiyelliler): {pareto.GroupBCount} ürün cironun %15'ini getiriyor. AI ile başlık ve tag optimizasyonu önerilir.");
        sb.AppendLine($"* 🔴 C Grubu (Yavaş Satanlar): {pareto.GroupCCount} ürün ciroya düşük katkı sağlıyor. Fotoğraf ve fiyat revizyonu yapılmalı.");

        sb.AppendLine("\n⚡ 3. Hızlı Satış Eylem Planı:");
        sb.AppendLine("1. Satışı düşen ürünlerde 'AI Optimizasyon' butonunu kullanarak 13 altın etiketi yenileyin.");
        sb.AppendLine("2. 'AI Görsel Studio' ile kapak fotoğraflarını 2000x2000 HD stüdyo fonuna taşıyın.");
        sb.AppendLine("3. Sepet tutarını (AOV) artırmak için 2'li paket varyasyonu (bundle) ekleyin.");
        return sb.ToString();
    }
}
