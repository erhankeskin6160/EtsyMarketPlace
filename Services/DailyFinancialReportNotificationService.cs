using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimilarProductsWinForms.Models;

namespace SimilarProductsWinForms.Services;

/// <summary>
/// Günlük Gece Finans Raporu (Telegram & Bildirim) Derleme ve Gönderim Servisi
/// </summary>
internal static class DailyFinancialReportNotificationService
{
    public static async Task<(bool Success, string Message)> SendDailyReportAsync(
        NotificationSettings? settings = null,
        decimal? customExchangeRate = null,
        bool isManualTrigger = false,
        CancellationToken ct = default)
    {
        settings ??= NotificationSettingsStore.Load();

        if (!settings.TelegramEnabled || string.IsNullOrWhiteSpace(settings.TelegramBotToken) || string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            return (false, "Telegram bildirimleri kapalı veya Bot Token / Chat ID eksik.");
        }

        try
        {
            // 1. Güncel döviz kurunu belirle
            decimal rate = customExchangeRate ?? await new ExchangeRateService().GetHistoricalRateAsync(DateTime.UtcNow);

            // 2. Bugünün ve Bu Ayın Finans Raporunu Çek
            var apiSettings = EtsyApiSettingsStore.Load();
            var reportService = new FinancialReportService();
            FinancialReport report;

            bool hasApi = apiSettings.HasApiCredentials && (!string.IsNullOrWhiteSpace(apiSettings.AccessToken) || !string.IsNullOrWhiteSpace(apiSettings.RefreshToken));
            var today = DateTime.Today;
            var from = new DateTimeOffset(new DateTime(today.Year, today.Month, 1), TimeZoneInfo.Local.GetUtcOffset(today));
            var to = new DateTimeOffset(today.AddDays(1).AddSeconds(-1), TimeZoneInfo.Local.GetUtcOffset(today));

            if (hasApi)
            {
                report = await reportService.GetReportAsync(apiSettings, from, to, rate, ct);
            }
            else
            {
                report = await FinancialReportService.GenerateMockReportAsync(from, to, rate);
            }

            // 3. Mesajı Formatla
            string messageHtml = FormatTelegramReportHtml(report, rate, isManualTrigger, settings.IncludeAiSummaryInNightReport);

            // 4. Telegram'a Gönder
            var (tgSuccess, tgMsg) = await NotificationService.SendTelegramMessageAsync(
                settings.TelegramBotToken,
                settings.TelegramChatId,
                messageHtml
            );

            if (tgSuccess)
            {
                settings.LastDailyFinancialReportSentDate = today.ToString("yyyy-MM-dd");
                NotificationSettingsStore.Save(settings);
            }

            return (tgSuccess, tgMsg);
        }
        catch (Exception ex)
        {
            return (false, $"Finans raporu gönderilemedi: {ex.Message}");
        }
    }

    public static string FormatTelegramReportHtml(
        FinancialReport report,
        decimal exchangeRate,
        bool isManualTrigger,
        bool includeAiSummary = true)
    {
        var now = DateTime.Now;
        var sb = new StringBuilder();

        // 1. Başlık
        string headerIcon = isManualTrigger ? "📊" : "🌙";
        string headerTitle = isManualTrigger ? "ETSY ANLIK FİNANSAL RAPOR" : "ETSY GÜNLÜK GECE FİNANS RAPORU";

        sb.AppendLine($"{headerIcon} <b>{headerTitle}</b>");
        sb.AppendLine($"📅 <i>{now:dd MMMM yyyy, dddd | HH:mm}</i>");
        sb.AppendLine("────────────────────────────");

        // 2. Bugünün Verileri (Bugünkü Fişler & Günlük Özet)
        var today = DateTime.Today;
        var todaySummary = report.DailySummaries.Find(d => d.SortDate.Date == today);
        var todayOrders = report.OrderSummaries.FindAll(o => o.OrderDate.LocalDateTime.Date == today);
        int todayOrderCount = todayOrders.Count;

        decimal displayGrossUSD = todaySummary?.GrossSales ?? (todayOrders.Count > 0 ? todayOrders.Sum(o => o.GrandTotal) : 0);
        decimal displayGrossTRY = Math.Round(displayGrossUSD * exchangeRate, 2);
        decimal todayFeesUSD = todaySummary != null ? (todaySummary.EtsyFees + todaySummary.InnerAdFees + todaySummary.OffsiteAdFees) : todayOrders.Sum(o => o.EtsyFees + o.OffsiteAdFee);
        decimal todayCostUSD = todaySummary?.ProductCosts ?? todayOrders.Sum(o => o.ProductCost);
        decimal displayProfitUSD = todaySummary?.RealNetProfitUSD ?? todayOrders.Sum(o => o.NetProfitUSD);
        decimal displayProfitTRY = todaySummary != null ? todaySummary.RealNetProfitTRY : Math.Round(displayProfitUSD * exchangeRate, 2);
        decimal todayMarginPct = displayGrossUSD > 0 ? Math.Round(displayProfitUSD / displayGrossUSD * 100, 1) : 0;

        sb.AppendLine($"🛍️ <b>Günün Siparişleri:</b> <code>{todayOrderCount} Adet</code>");
        sb.AppendLine($"💰 <b>Brüt Satış (Ciro):</b> ₺{displayGrossTRY:N2} (<code>${displayGrossUSD:N2}</code>)");
        if (todayOrderCount > 0 || displayGrossUSD > 0)
        {
            sb.AppendLine($"📋 <b>Etsy Kesintileri:</b> -₺{todayFeesUSD * exchangeRate:N2} (<code>-${todayFeesUSD:N2}</code>)");
            sb.AppendLine($"📦 <b>Ürün Maliyeti:</b> -₺{todayCostUSD * exchangeRate:N2} (<code>-${todayCostUSD:N2}</code>)");
            sb.AppendLine($"💵 <b>GÜNÜN NET KÂRI:</b> <b>₺{displayProfitTRY:N2} (${displayProfitUSD:N2})</b>");
            sb.AppendLine($"🎯 <b>Günün Kâr Marjı:</b> %{todayMarginPct:N1}");
        }
        else
        {
            sb.AppendLine("ℹ️ <i>Bugün henüz yeni sipariş kaydı oluşmadı.</i>");
        }
        sb.AppendLine("────────────────────────────");

        // 3. Bu Ayın Kümülatif Durumu (Month-to-Date)
        decimal monthGrossUSD = report.TotalGross;
        decimal monthGrossTRY = Math.Round(monthGrossUSD * exchangeRate, 2);
        decimal monthProfitUSD = report.RealNetProfitUSD;
        decimal monthProfitTRY = Math.Round(monthProfitUSD * exchangeRate, 2);
        decimal monthMarginPct = report.ProfitMarginPct;

        string monthName = now.ToString("MMMM", CultureInfo.GetCultureInfo("tr-TR"));
        sb.AppendLine($"📈 <b>Aylık Kümülatif ({monthName}):</b>");
        sb.AppendLine($"• <b>Toplam Ciro:</b> ₺{monthGrossTRY:N0} (<code>${monthGrossUSD:N0}</code>)");
        sb.AppendLine($"• <b>Gerçek Net Kâr:</b> ₺{monthProfitTRY:N0} (<code>${monthProfitUSD:N0}</code>)");
        sb.AppendLine($"• <b>Net Kâr Marjı:</b> %{monthMarginPct:N1}");
        sb.AppendLine($"• <b>Toplam Sipariş:</b> {report.OrderSummaries.Count} Adet");

        // 4. Gelecek Ay Tahmini & Atölye İhtiyacı
        var forecast = FinancialForecastingService.GenerateForecast(report, exchangeRate);
        sb.AppendLine("────────────────────────────");
        sb.AppendLine($"🔮 <b>Gelecek Ay Beklenti:</b> ₺{forecast.NextMonthGrossTRY:N0} (<code>${forecast.NextMonthGrossUSD:N0}</code>)");
        sb.AppendLine($"💵 <b>Tahmini Net Kâr:</b> ₺{forecast.NextMonthNetProfitTRY:N0} (<code>${forecast.NextMonthNetProfitUSD:N0}</code>)");
        sb.AppendLine($"🖨️ <b>Hammadde İhtiyacı:</b> ~{forecast.EstimatedFilamentKg:N1} kg Filament | {forecast.EstimatedPackagingBoxes} Kutu");

        // 5. AI Stratejik Tavsiyesi
        if (includeAiSummary && forecast.AiRecommendations.Count > 0)
        {
            sb.AppendLine("────────────────────────────");
            string firstRec = forecast.AiRecommendations[0].Replace("**", "");
            sb.AppendLine($"🤖 <b>Yapay Zekâ İçgörüsü:</b>\n<i>\"{firstRec}\"</i>");
        }

        sb.AppendLine("────────────────────────────");
        sb.AppendLine($"🇹🇷 <i>Döviz Kuru: $1 = ₺{exchangeRate:N2}</i>");
        sb.AppendLine("<i>⚡ Etsy Marketplace 7/24 VDS Engine</i>");

        return sb.ToString();
    }
}
