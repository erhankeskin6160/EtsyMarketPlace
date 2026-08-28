using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SimilarProductsWinForms.Models;

namespace SimilarProductsWinForms.Services;

/// <summary>
/// AI Destekli E-Ticaret Finansal Tahminleme (AI Forecasting) Servisi
/// Zaman serisi analizi, üstel düzeltme ve Etsy küresel e-ticaret mevsimsellik modellerini kullanır.
/// </summary>
internal static class FinancialForecastingService
{
    // Etsy Global E-Ticaret Aylık Mevsimsellik İndeksi (Seasonal Weight Factors)
    private static readonly Dictionary<int, double> SeasonalityWeights = new()
    {
        { 1, 0.85 },  // Ocak (Tatil sonrası sakinlik)
        { 2, 0.90 },  // Şubat (Sevgililer Günü etkisi)
        { 3, 0.95 },  // Mart (Bahar başlangıcı)
        { 4, 1.05 },  // Nisan (Paskalya / Bahar yenilenmesi)
        { 5, 1.18 },  // Mayıs (Anneler Günü zirvesi)
        { 6, 1.00 },  // Haziran (Yaz başlangıcı)
        { 7, 0.92 },  // Temmuz (Yaz durgunluğu)
        { 8, 0.95 },  // Ağustos (Okula dönüş hazırlığı)
        { 9, 1.10 },  // Eylül (Sonbahar / Cadılar Bayramı siparişleri)
        { 10, 1.25 }, // Ekim (Cadılar Bayramı & Erken Yılbaşı)
        { 11, 1.45 }, // Kasım (Black Friday / Cyber Week patlaması)
        { 12, 1.55 }, // Aralık (Noel & Yılbaşı Zirvesi)
    };

    /// <summary>
    /// Geçmiş finansal rapor verilerinden gelecek aylar için ciro ve net kâr projeksiyonu üretir.
    /// </summary>
    public static FinancialForecastResult GenerateForecast(FinancialReport report, decimal exchangeRate)
    {
        var months = report.MonthlySummaries.OrderBy(m => m.SortDate).ToList();

        // Eğer aylık özet yoksa varsayılan değerlerle fallback üret
        if (months.Count == 0)
        {
            return GenerateFallbackForecast(exchangeRate);
        }

        // Geçmiş veri noktaları zaman çizelgesine eklenir
        var timeline = new List<ForecastDataPoint>();
        foreach (var m in months)
        {
            timeline.Add(new ForecastDataPoint(
                m.PeriodLabel,
                m.SortDate,
                m.GrossSales,
                m.RealNetProfitUSD,
                m.RealNetProfitTRY,
                m.RealNetProfitUSD * 0.85m,
                m.RealNetProfitUSD * 1.15m,
                (int)Math.Max(1, m.GrossSales / (report.AverageOrderValue > 0 ? report.AverageOrderValue : 25m)),
                IsHistorical: true
            ));
        }

        // Zaman serisi trend ve büyüme hızı analizi
        decimal totalGross = report.TotalGross;
        decimal totalRefunds = report.TotalRefunds;
        decimal totalFees = report.TotalFees + report.TotalInnerAdFees + report.TotalOffsiteAdFees;
        decimal totalCosts = report.TotalProductCosts;

        // Aşırı İade Anomalisi Tespiti (Outlier Filter):
        // Eğer dönem içindeki iade oranı %15'ten yüksekse (örneğin %80 iptal/iade),
        // geleceğe de bu anormal zararın yansıması engellenir; standart %3.5 e-ticaret iade payı ile operasyonel kâr hesaplanır.
        decimal refundRate = totalGross > 0 ? (totalRefunds / totalGross) : 0m;
        decimal operationalMarginPct;
        bool hasRefundAnomaly = refundRate > 0.15m;

        if (hasRefundAnomaly)
        {
            decimal normalizedRefunds = totalGross * 0.035m;
            decimal operationalProfitUSD = totalGross - totalFees - normalizedRefunds - totalCosts;
            operationalMarginPct = totalGross > 0 ? (operationalProfitUSD / totalGross) : 0.42m;
            operationalMarginPct = Math.Clamp(operationalMarginPct, 0.30m, 0.55m);
        }
        else
        {
            decimal actualProfit = report.RealNetProfitUSD;
            operationalMarginPct = totalGross > 0 ? (actualProfit / totalGross) : 0.40m;
            operationalMarginPct = Math.Clamp(operationalMarginPct, 0.25m, 0.60m);
        }

        // Son 3 ayın ağırlıklı büyüme trendi (Momentum)
        decimal growthMoM = 0.05m; // Varsayılan %5 organik büyüme
        if (months.Count >= 2)
        {
            var last = months[^1].GrossSales;
            var prev = months[^2].GrossSales;
            if (prev > 0)
            {
                growthMoM = Math.Clamp((last - prev) / prev, -0.30m, 0.50m);
            }
        }

        DateTime lastDate = months[^1].SortDate;
        DateTime nextDate1 = lastDate.AddMonths(1);
        DateTime nextDate2 = lastDate.AddMonths(2);

        double lastSeasonal = SeasonalityWeights.GetValueOrDefault(lastDate.Month, 1.0);
        double next1Seasonal = SeasonalityWeights.GetValueOrDefault(nextDate1.Month, 1.0);
        double next2Seasonal = SeasonalityWeights.GetValueOrDefault(nextDate2.Month, 1.0);

        // Gelecek 1. Ay (Önümüzdeki Ay) Projeksiyonu
        decimal baseNext1Gross = months[^1].GrossSales * (1m + growthMoM * 0.7m);
        decimal seasonalMultiplier1 = (decimal)(next1Seasonal / Math.Max(0.5, lastSeasonal));
        decimal next1GrossUSD = Math.Max(100, Math.Round(baseNext1Gross * seasonalMultiplier1, 2));
        decimal next1ProfitUSD = Math.Max(50, Math.Round(next1GrossUSD * operationalMarginPct, 2));

        // Güven Aralıkları (Kötü Senaryo: %85, İyimser Senaryo: %120)
        decimal lowProfitUSD = Math.Round(next1ProfitUSD * 0.85m, 2);
        decimal highProfitUSD = Math.Round(next1ProfitUSD * 1.20m, 2);

        decimal avgAov = report.AverageOrderValue > 0 ? report.AverageOrderValue : 28m;
        int next1Orders = (int)Math.Max(1, Math.Round(next1GrossUSD / avgAov));

        // Gelecek 1. ayı zaman çizelgesine ekle
        string next1Label = nextDate1.ToString("MMM yyyy", CultureInfo.GetCultureInfo("tr-TR"));
        timeline.Add(new ForecastDataPoint(
            next1Label + " (Tahmin)",
            nextDate1,
            next1GrossUSD,
            next1ProfitUSD,
            Math.Round(next1ProfitUSD * exchangeRate, 2),
            lowProfitUSD,
            highProfitUSD,
            next1Orders,
            IsHistorical: false
        ));

        // Gelecek 2. Ay Projeksiyonu
        decimal seasonalMultiplier2 = (decimal)(next2Seasonal / Math.Max(0.5, next1Seasonal));
        decimal next2GrossUSD = Math.Max(100, Math.Round(next1GrossUSD * (1m + growthMoM * 0.5m) * seasonalMultiplier2, 2));
        decimal next2ProfitUSD = Math.Max(50, Math.Round(next2GrossUSD * operationalMarginPct, 2));
        string next2Label = nextDate2.ToString("MMM yyyy", CultureInfo.GetCultureInfo("tr-TR"));

        timeline.Add(new ForecastDataPoint(
            next2Label + " (Tahmin)",
            nextDate2,
            next2GrossUSD,
            next2ProfitUSD,
            Math.Round(next2ProfitUSD * exchangeRate, 2),
            Math.Round(next2ProfitUSD * 0.85m, 2),
            Math.Round(next2ProfitUSD * 1.20m, 2),
            (int)Math.Max(1, Math.Round(next2GrossUSD / avgAov)),
            IsHistorical: false
        ));

        // 3D Baskı Hammadde & Kutu İhtiyacı Hesabı (Sipariş Başına Ortalama 110g Filament + %15 Destek/Fire)
        double estimatedFilamentKg = Math.Round(next1Orders * 0.125, 1);
        int estimatedBoxes = (int)Math.Ceiling(next1Orders * 1.05);

        // AI Stratejik Tavsiyeleri
        var insights = GenerateAiInsights(
            next1GrossUSD, next1ProfitUSD, exchangeRate, next1Orders,
            growthMoM, nextDate1.Month, estimatedFilamentKg, estimatedBoxes, operationalMarginPct, hasRefundAnomaly, refundRate);

        return new FinancialForecastResult(
            NextMonthGrossUSD: next1GrossUSD,
            NextMonthGrossTRY: Math.Round(next1GrossUSD * exchangeRate, 2),
            NextMonthNetProfitUSD: next1ProfitUSD,
            NextMonthNetProfitTRY: Math.Round(next1ProfitUSD * exchangeRate, 2),
            LowScenarioUSD: lowProfitUSD,
            LowScenarioTRY: Math.Round(lowProfitUSD * exchangeRate, 2),
            HighScenarioUSD: highProfitUSD,
            HighScenarioTRY: Math.Round(highProfitUSD * exchangeRate, 2),
            NextMonthOrders: next1Orders,
            GrowthRateMoM: Math.Round(growthMoM * 100, 1),
            EstimatedFilamentKg: estimatedFilamentKg,
            EstimatedPackagingBoxes: estimatedBoxes,
            Timeline: timeline,
            AiRecommendations: insights
        );
    }

    private static List<string> GenerateAiInsights(
        decimal grossUSD,
        decimal profitUSD,
        decimal rate,
        int orders,
        decimal growthMoM,
        int month,
        double filamentKg,
        int boxes,
        decimal marginPct,
        bool hasRefundAnomaly,
        decimal refundRate)
    {
        var list = new List<string>();

        // 1. Ciro & Kâr Beklentisi
        string growthText = growthMoM >= 0 ? $"+%{growthMoM * 100:N1} büyüme" : $"-%{Math.Abs(growthMoM) * 100:N1} daralma";
        list.Add($"🔮 **Gelecek Ay Projeksiyonu:** Mevcut sipariş ritminiz ve sezonluk trende göre yaklaşık **{orders} sipariş**, **${grossUSD:N0} ciro** ve **₺{profitUSD * rate:N0} (${profitUSD:N0}) Gerçek Net Kâr** beklenmektedir ({growthText}).");

        // 2. Aşırı İade Anomalisi Bilgilendirmesi (Varsa)
        if (hasRefundAnomaly)
        {
            list.Add($"🛡️ **İade Düzeltmeli Operasyonel Kâr:** Son dönemdeki istisnai iade tutarı (%{refundRate * 100:N0}) filtrelenmiş olup, projeksiyon dükkanınızın sağlıklı **%{marginPct * 100:N1}** operasyonel üretim kâr marjı üzerinden hesaplanmıştır.");
        }

        // 3. Sezonluk E-Ticaret Uyarısı
        if (month is 10 or 11 or 12)
        {
            list.Add("🚀 **Q4 Yılbaşı & Black Friday Zirvesi:** Yılın en yüksek satış hacimli çeyreğindesiniz! Küresel Etsy trafiği %40'a varan oranda artış göstermektedir. Reklam bütçelerinizi erken artırıp kargo sürelerinizi optimize etmeniz önerilir.");
        }
        else if (month is 4 or 5)
        {
            list.Add("🌸 **Bahar & Anneler Günü Dalgası:** Hediyeleşme talebinin arttığı bir döneme giriyorsunuz. Kişiselleştirilmiş ürün varyantlarınızı öne çıkarmanız kâr marjınızı %10-15 yukarı taşıyabilir.");
        }
        else if (month is 7 or 8)
        {
            list.Add("☀️ **Yaz Sezonu Dengesi:** Yaz aylarındaki genel e-ticaret durgunluğunu aşmak için yeni ürün tasarımları ve taslak listelemeleri hazırlamak için en ideal dönemdesiniz.");
        }

        // 4. 3D Baskı / Üretim & Stok Tavsiyesi
        list.Add($"📦 **Hammadde & Kutu Stoğu:** Önümüzdeki ay beklenen {orders} siparişin zamanında teslimatı için atölyenizde en az **~{filamentKg:N1} kg filament** ve **{boxes} adet kargo ambalaj kutusu** bulundurmanız önerilir.");

        // 5. Kâr Marjı & Fiyatlandırma Stratejisi
        decimal marginDisplay = Math.Round(marginPct * 100, 1);
        if (marginDisplay >= 45)
        {
            list.Add($"🎯 **Güçlü Kâr Marjı (%{marginDisplay:N1}):** Mağazanız sağlıklı bir kâr oranına sahip. Reklam harcamalarınızı kontrollü şekilde artırarak sipariş adedini ölçekleyebilirsiniz.");
        }
        else
        {
            list.Add($"⚠️ **Kâr Marjı İyileştirme Fırsatı (%{marginDisplay:N1}):** Kâr marjınızı %50 seviyesine çıkarmak için ürün maliyetlerini gözden geçirebilir veya popüler ürünlerde $1.00 - $2.00 fiyat artışı simüle edebilirsiniz.");
        }

        return list;
    }

    public static async Task<List<string>> GenerateDeepAiInsightsAsync(
        FinancialReport report,
        FinancialForecastResult forecast,
        decimal exchangeRate,
        AiOptimizationSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.IsOffline || (!settings.UseOpenAi && !settings.UseGemini))
        {
            return forecast.AiRecommendations.ToList();
        }

        try
        {
            var prompt = BuildCfoPrompt(report, forecast, exchangeRate);

            if (settings.UseGemini)
            {
                return await FetchGeminiCfoInsightsAsync(settings, prompt, cancellationToken);
            }

            if (settings.UseOpenAi)
            {
                return await FetchOpenAiCfoInsightsAsync(settings, prompt, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            var fallback = forecast.AiRecommendations.ToList();
            fallback.Insert(0, $"⚠️ Canlı AI analizi alınamadı ({ex.Message}). Yerel finansal model tavsiyeleri gösteriliyor:");
            return fallback;
        }

        return forecast.AiRecommendations.ToList();
    }

    private static string BuildCfoPrompt(FinancialReport report, FinancialForecastResult forecast, decimal exchangeRate)
    {
        decimal marginPct = report.TotalGross > 0 ? Math.Round((report.RealNetProfitUSD / report.TotalGross) * 100, 1) : 40m;
        return
            $$"""
            Etsy Mağazası Finansal ve Operasyonel Özeti:
            - Toplam Brüt Satış: ${{report.TotalGross:N2}} (₺{{report.TotalGross * exchangeRate:N0}})
            - Gerçek Net Kâr: ${{report.RealNetProfitUSD:N2}} (₺{{report.RealNetProfitTRY:N0}})
            - Net Kâr Marjı: %{{marginPct:N1}}
            - Etsy Kesintileri ve Komisyon: ${{report.TotalFees:N2}}
            - İç & Dış Reklam Harcamaları: ${{report.TotalInnerAdFees + report.TotalOffsiteAdFees:N2}}
            - İadeler: ${{report.TotalRefunds:N2}}
            - Gelecek Ay Beklenen Ciro: ${{forecast.NextMonthGrossUSD:N0}} (₺{{forecast.NextMonthGrossTRY:N0}})
            - Gelecek Ay Beklenen Net Kâr: ${{forecast.NextMonthNetProfitUSD:N0}} (₺{{forecast.NextMonthNetProfitTRY:N0}})
            - Gelecek Ay Beklenen Sipariş: ~{{forecast.NextMonthOrders}} Adet (Aylık Büyüme: %{{forecast.GrowthRateMoM:N1}})
            - 3D Baskı Tahmini Filament Tüketimi: ~{{forecast.EstimatedFilamentKg:N1}} kg, ~{{forecast.EstimatedPackagingBoxes}} Adet Kargo Kutusu

            Görev: Sen uzman bir Etsy Finans Direktörü (CFO) ve E-Ticaret Büyüme Danışmanısın.
            Bu mağaza için 4 veya 5 adet son derece somut, kârı ve satışı artıracak stratejik tavsiye üret.
            Her madde mutlaka bir emoji ve kalın başlıkla başlasın (örn: 🔮 **Büyüme & Ciro Hedefi:**, 🎯 **Fiyatlandırma & Kâr Marjı:**, 📦 **3D Baskı Stok & Atölye:**, 🚀 **Reklam & Q4 Sezon Stratejisi:**).

            Yalnızca aşağıdaki JSON şemasında geçerli bir JSON yanıtı döndür:
            {
              "recommendations": [
                "madde 1",
                "madde 2",
                "madde 3",
                "madde 4"
              ]
            }
            """;
    }

    private static async Task<List<string>> FetchOpenAiCfoInsightsAsync(
        AiOptimizationSettings settings,
        string prompt,
        CancellationToken cancellationToken)
    {
        using var client = new System.Net.Http.HttpClient();
        using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.OpenAiApiKey.Trim());

        var payload = new
        {
            model = string.IsNullOrWhiteSpace(settings.OpenAiModel) ? "gpt-5.5" : settings.OpenAiModel.Trim(),
            instructions = "Sen profesyonel bir Etsy Finans Direktörü (CFO) ve E-Ticaret Büyüme Danışmanısın. Sadece geçerli JSON yanıtı döndür.",
            input = prompt
        };

        request.Content = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI HTTP {(int)response.StatusCode}: {body}");
        }

        return ExtractRecommendationsFromJson(body);
    }

    private static async Task<List<string>> FetchGeminiCfoInsightsAsync(
        AiOptimizationSettings settings,
        string prompt,
        CancellationToken cancellationToken)
    {
        using var client = new System.Net.Http.HttpClient();
        using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions");
        request.Headers.Add("x-goog-api-key", settings.GeminiApiKey.Trim());

        var payload = new
        {
            model = string.IsNullOrWhiteSpace(settings.GeminiModel) ? "gemini-3.7-flash" : settings.GeminiModel.Trim(),
            system_instruction = "Sen profesyonel bir Etsy Finans Direktörü (CFO) ve E-Ticaret Büyüme Danışmanısın. Sadece geçerli JSON yanıtı döndür.",
            input = prompt,
            generation_config = new { temperature = 0.6 }
        };

        request.Content = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini HTTP {(int)response.StatusCode}: {body}");
        }

        return ExtractRecommendationsFromJson(body);
    }

    private static List<string> ExtractRecommendationsFromJson(string responseBody)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
        string text = "";

        if (doc.RootElement.TryGetProperty("output_text", out var outProp))
        {
            text = outProp.GetString() ?? "";
        }
        else if (doc.RootElement.TryGetProperty("output", out var outputArray))
        {
            foreach (var item in outputArray.EnumerateArray())
            {
                if (item.TryGetProperty("content", out var contentArray))
                {
                    foreach (var c in contentArray.EnumerateArray())
                    {
                        if (c.TryGetProperty("text", out var textProp))
                        {
                            text = textProp.GetString() ?? "";
                            break;
                        }
                    }
                }
            }
        }

        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLine = text.IndexOf('\n');
            if (firstLine >= 0) text = text[(firstLine + 1)..];
            var fence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) text = text[..fence].Trim();
        }

        using var jsonDoc = System.Text.Json.JsonDocument.Parse(text);
        if (jsonDoc.RootElement.TryGetProperty("recommendations", out var recs) && recs.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var r in recs.EnumerateArray())
            {
                var val = r.GetString();
                if (!string.IsNullOrWhiteSpace(val)) list.Add(val);
            }
            if (list.Count > 0) return list;
        }

        throw new InvalidOperationException("JSON yanıtı recommendations dizisi içermiyor.");
    }

    private static FinancialForecastResult GenerateFallbackForecast(decimal exchangeRate)
    {
        return new FinancialForecastResult(
            NextMonthGrossUSD: 2500,
            NextMonthGrossTRY: 2500 * exchangeRate,
            NextMonthNetProfitUSD: 1100,
            NextMonthNetProfitTRY: 1100 * exchangeRate,
            LowScenarioUSD: 900,
            LowScenarioTRY: 900 * exchangeRate,
            HighScenarioUSD: 1400,
            HighScenarioTRY: 1400 * exchangeRate,
            NextMonthOrders: 90,
            GrowthRateMoM: 5.0m,
            EstimatedFilamentKg: 10.5,
            EstimatedPackagingBoxes: 95,
            Timeline: new List<ForecastDataPoint>(),
            AiRecommendations: new List<string> { "Yeterli veri toplandıkça yapay zekâ tahminleri daha hassas hale gelecektir." }
        );
    }
}
