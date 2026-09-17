namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;

public static class OpenAiUsageFetcherService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(25) };

    public static async Task<OpenAiOfficialUsageReport> FetchOfficialUsageReportAsync(
        string apiKey,
        string? adminApiKey = null,
        int? year = null,
        int? month = null,
        int? lastDays = null,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var cultureTr = new CultureInfo("tr-TR");
        DateTime startDate;
        DateTime endDate;
        string periodLabel;

        if (lastDays.HasValue && lastDays.Value > 0)
        {
            endDate = DateTime.UtcNow.Date.AddDays(1);
            startDate = DateTime.UtcNow.Date.AddDays(-lastDays.Value);
            periodLabel = $"Son {lastDays.Value} Gün";
        }
        else
        {
            int targetYear = year ?? DateTime.UtcNow.Year;
            int targetMonth = month ?? DateTime.UtcNow.Month;
            startDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            endDate = startDate.AddMonths(1);
            periodLabel = startDate.ToString("MMMM yyyy", cultureTr);
        }

        string? effectiveAdminKey = !string.IsNullOrWhiteSpace(adminApiKey)
            ? adminApiKey.Trim()
            : (apiKey.Trim().StartsWith("sk-admin-", StringComparison.OrdinalIgnoreCase) ? apiKey.Trim() : null);

        string cacheKey = $"report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{(effectiveAdminKey != null ? "admin" : "std")}";
        bool isPastMonth = endDate <= DateTime.UtcNow.Date;

        return await AiDataCacheService.GetOrFetchAsync<OpenAiOfficialUsageReport>(
            provider: "OpenAI",
            cacheKey: cacheKey,
            category: "usage_report",
            ttl: isPastMonth ? TimeSpan.FromDays(7) : TimeSpan.FromMinutes(10),
            isFinalized: isPastMonth,
            fetcher: () => FetchOfficialUsageReportInternalAsync(apiKey, effectiveAdminKey, startDate, endDate, periodLabel, ct),
            forceRefresh: forceRefresh,
            ct: ct);
    }

    private static async Task<OpenAiOfficialUsageReport> FetchOfficialUsageReportInternalAsync(
        string apiKey,
        string? effectiveAdminKey,
        DateTime startDate,
        DateTime endDate,
        string periodLabel,
        CancellationToken ct)
    {
        var report = new OpenAiOfficialUsageReport
        {
            Year = startDate.Year,
            Month = startDate.Month,
            MonthName = periodLabel,
            MaskedKey = AiPriceCalculator.MaskApiKey(apiKey)
        };

        long startUnix = new DateTimeOffset(startDate).ToUnixTimeSeconds();
        long endUnix = new DateTimeOffset(endDate).ToUnixTimeSeconds();

        // 1. Admin Key ile OpenAI resmi organizasyon uç noktalarını çek
        if (!string.IsNullOrWhiteSpace(effectiveAdminKey))
        {
            try
            {
                report.HasAdminKey = true;
                report.MaskedKey = AiPriceCalculator.MaskApiKey(effectiveAdminKey);

                // Costs API (bucket_width=1d ve limit=31 ile ayın günlerini çek, next_page sayfalamasını takip et)
                string? nextCostPage = null;
                int costPages = 0;
                do
                {
                    string costsUrl = $"https://api.openai.com/v1/organization/costs?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31"
                        + (!string.IsNullOrEmpty(nextCostPage) ? $"&next_page={Uri.EscapeDataString(nextCostPage)}" : "");

                    using var costReq = new HttpRequestMessage(HttpMethod.Get, costsUrl);
                    costReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveAdminKey);

                    using var costResp = await HttpClient.SendAsync(costReq, ct);
                    if (costResp.IsSuccessStatusCode)
                    {
                        string costBody = await costResp.Content.ReadAsStringAsync(ct);
                        var (costItems, nextCursor) = AiPriceCalculator.ParseOpenAiCostsDetailsJson(costBody);
                        foreach (var item in costItems)
                        {
                            report.DailyItems.Add(item);
                            report.TotalCostUsd += item.CostUsd;
                        }
                        nextCostPage = nextCursor;
                    }
                    else
                    {
                        string err = await costResp.Content.ReadAsStringAsync(ct);
                        report.ErrorMessage = $"Costs API Hatası ({costResp.StatusCode}): {err}";
                        break;
                    }
                    costPages++;
                } while (!string.IsNullOrEmpty(nextCostPage) && costPages < 10);

                // Completions Usage API (bucket_width=1d ve limit=31 ile token ve istek sayılarını çek)
                string? nextUsagePage = null;
                int usagePages = 0;
                do
                {
                    string usageUrl = $"https://api.openai.com/v1/organization/usage/completions?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31"
                        + (!string.IsNullOrEmpty(nextUsagePage) ? $"&next_page={Uri.EscapeDataString(nextUsagePage)}" : "");

                    using var usageReq = new HttpRequestMessage(HttpMethod.Get, usageUrl);
                    usageReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveAdminKey);

                    using var usageResp = await HttpClient.SendAsync(usageReq, ct);
                    if (usageResp.IsSuccessStatusCode)
                    {
                        string usageBody = await usageResp.Content.ReadAsStringAsync(ct);
                        nextUsagePage = AiPriceCalculator.MergeOpenAiUsageJson(report, usageBody, "gpt-4o");
                    }
                    else
                    {
                        string err = await usageResp.Content.ReadAsStringAsync(ct);
                        report.ErrorMessage = (report.ErrorMessage == null ? "" : report.ErrorMessage + " | ") + $"Usage API Hatası ({usageResp.StatusCode}): {err}";
                        break;
                    }
                    usagePages++;
                } while (!string.IsNullOrEmpty(nextUsagePage) && usagePages < 10);

                // Embeddings Usage API (Embeddings varsa girdi tokenlarını rapora ekle)
                try
                {
                    string embUrl = $"https://api.openai.com/v1/organization/usage/embeddings?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31";
                    using var embReq = new HttpRequestMessage(HttpMethod.Get, embUrl);
                    embReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveAdminKey);

                    using var embResp = await HttpClient.SendAsync(embReq, ct);
                    if (embResp.IsSuccessStatusCode)
                    {
                        string embBody = await embResp.Content.ReadAsStringAsync(ct);
                        AiPriceCalculator.MergeOpenAiUsageJson(report, embBody, "Embeddings");
                    }
                }
                catch { }

                report.DataSource = "OpenAI Admin API (Canlı Resmi Fatura)";
                return report;
            }
            catch (Exception ex)
            {
                report.ErrorMessage = $"Admin API çağrısı sırasında hata: {ex.Message}";
            }
        }

        // 2. Admin Key yoksa: Yerel SQLite veritabanındaki OpenAI kayıtlarını derle
        report.HasAdminKey = false;
        report.DataSource = "OpenAI Standart API + Yerel Sayaç";
        if (string.IsNullOrWhiteSpace(report.ErrorMessage))
        {
            report.ErrorMessage = "Admin API Key tanımlanmadığı için OpenAI platform genel faturası yerine bu projedeki gerçek token ve işlem geçmişi listelenmektedir.";
        }

        try
        {
            var repo = AiTokenUsageTrackerService.GetRepository();
            var history = await repo.GetHistoryAsync("OpenAI", since: new DateTimeOffset(startDate), limit: 1000);

            var grouped = history
                .GroupBy(h => new { Date = h.Timestamp.Date, Model = h.ModelName })
                .OrderByDescending(g => g.Key.Date)
                .ToList();

            foreach (var g in grouped)
            {
                var dailyItem = new OpenAiDailyUsageItem
                {
                    Date = g.Key.Date,
                    ServiceOrModel = string.IsNullOrWhiteSpace(g.Key.Model) ? "gpt-4o" : g.Key.Model,
                    RequestCount = g.Count(),
                    InputTokens = g.Sum(x => x.PromptTokens),
                    OutputTokens = g.Sum(x => x.CompletionTokens),
                    CostUsd = g.Sum(x => x.EstimatedCostUsd)
                };

                report.DailyItems.Add(dailyItem);
                report.TotalCostUsd += dailyItem.CostUsd;
                report.TotalTokens += dailyItem.TotalTokens;
                report.TotalRequests += dailyItem.RequestCount;
            }
        }
        catch { }

        return report;
    }

    public static List<OpenAiDailyUsageItem> ParseCostsJson(string json)
    {
        return AiPriceCalculator.ParseOpenAiCostsDetailsJson(json).Items;
    }

    public static Task<OpenAiOfficialUsageReport> FetchCurrentMonthUsageAsync(
        string apiKey,
        string? adminApiKey = null,
        CancellationToken ct = default)
    {
        return FetchOfficialUsageReportAsync(apiKey, adminApiKey, ct: ct);
    }
}
