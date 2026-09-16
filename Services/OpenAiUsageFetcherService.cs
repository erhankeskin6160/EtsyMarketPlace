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
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<OpenAiOfficialUsageReport> FetchOfficialUsageReportAsync(
        string apiKey,
        string? adminApiKey = null,
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {
        int targetYear = year ?? DateTime.UtcNow.Year;
        int targetMonth = month ?? DateTime.UtcNow.Month;
        var cultureTr = new CultureInfo("tr-TR");
        string monthName = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy", cultureTr);

        var report = new OpenAiOfficialUsageReport
        {
            Year = targetYear,
            Month = targetMonth,
            MonthName = monthName,
            MaskedKey = AiPriceCalculator.MaskApiKey(apiKey)
        };

        var startDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        long startUnix = new DateTimeOffset(startDate).ToUnixTimeSeconds();
        long endUnix = new DateTimeOffset(endDate).ToUnixTimeSeconds();

        string? effectiveAdminKey = !string.IsNullOrWhiteSpace(adminApiKey)
            ? adminApiKey.Trim()
            : (apiKey.Trim().StartsWith("sk-admin-", StringComparison.OrdinalIgnoreCase) ? apiKey.Trim() : null);

        // 1. Admin Key varsa doğrudan OpenAI resmi organizasyon uç noktalarını çek
        if (!string.IsNullOrWhiteSpace(effectiveAdminKey))
        {
            try
            {
                report.HasAdminKey = true;
                report.MaskedKey = AiPriceCalculator.MaskApiKey(effectiveAdminKey);

                // Costs API
                string costsUrl = $"https://api.openai.com/v1/organization/costs?start_time={startUnix}&end_time={endUnix}&bucket_width=1d";
                using var costReq = new HttpRequestMessage(HttpMethod.Get, costsUrl);
                costReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveAdminKey);

                using var costResp = await HttpClient.SendAsync(costReq, ct);
                if (costResp.IsSuccessStatusCode)
                {
                    string costBody = await costResp.Content.ReadAsStringAsync(ct);
                    var costItems = ParseCostsJson(costBody);
                    foreach (var item in costItems)
                    {
                        report.DailyItems.Add(item);
                        report.TotalCostUsd += item.CostUsd;
                    }
                }

                // Completions Usage API
                string usageUrl = $"https://api.openai.com/v1/organization/usage/completions?start_time={startUnix}&end_time={endUnix}";
                using var usageReq = new HttpRequestMessage(HttpMethod.Get, usageUrl);
                usageReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveAdminKey);

                using var usageResp = await HttpClient.SendAsync(usageReq, ct);
                if (usageResp.IsSuccessStatusCode)
                {
                    string usageBody = await usageResp.Content.ReadAsStringAsync(ct);
                    MergeUsageJson(report, usageBody);
                }

                report.DataSource = "OpenAI Admin API (Canlı Resmi Fatura)";
                return report;
            }
            catch (Exception ex)
            {
                report.ErrorMessage = $"Admin API çağrısı sırasında hata: {ex.Message}";
            }
        }

        // 2. Admin Key yoksa veya başarısız olduysa: Yerel SQLite veritabanından OpenAI kullanımını derle
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
        var items = new List<OpenAiDailyUsageItem>();
        if (string.IsNullOrWhiteSpace(json)) return items;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataEl.EnumerateArray())
                {
                    var daily = new OpenAiDailyUsageItem();
                    if (item.TryGetProperty("timestamp", out var tsEl) && tsEl.TryGetInt64(out long ts))
                    {
                        daily.Date = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.Date;
                    }
                    else if (item.TryGetProperty("start_time", out var stEl) && stEl.TryGetInt64(out long st))
                    {
                        daily.Date = DateTimeOffset.FromUnixTimeSeconds(st).UtcDateTime.Date;
                    }
                    else
                    {
                        daily.Date = DateTime.UtcNow.Date;
                    }

                    if (item.TryGetProperty("line_item", out var lineEl))
                        daily.ServiceOrModel = lineEl.GetString() ?? "Genel API";

                    if (item.TryGetProperty("amount", out var amtEl) && amtEl.TryGetProperty("value", out var valEl))
                    {
                        if (valEl.ValueKind == JsonValueKind.Number && valEl.TryGetDecimal(out var val))
                            daily.CostUsd = val;
                        else if (valEl.ValueKind == JsonValueKind.String && decimal.TryParse(valEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dVal))
                            daily.CostUsd = dVal;
                    }

                    items.Add(daily);
                }
            }
        }
        catch { }

        return items;
    }

    public static void MergeUsageJson(OpenAiOfficialUsageReport report, string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var bucket in dataEl.EnumerateArray())
                {
                    long inTokens = bucket.TryGetProperty("input_tokens", out var inEl) ? inEl.GetInt64() : 0;
                    long outTokens = bucket.TryGetProperty("output_tokens", out var outEl) ? outEl.GetInt64() : 0;
                    int reqs = bucket.TryGetProperty("num_model_requests", out var reqEl) ? reqEl.GetInt32() : 0;

                    report.TotalTokens += (inTokens + outTokens);
                    report.TotalRequests += reqs;

                    if (bucket.TryGetProperty("start_time", out var stEl) && stEl.TryGetInt64(out long st))
                    {
                        var bucketDate = DateTimeOffset.FromUnixTimeSeconds(st).UtcDateTime.Date;
                        var existing = report.DailyItems.FirstOrDefault(x => x.Date == bucketDate);
                        if (existing != null)
                        {
                            existing.InputTokens += inTokens;
                            existing.OutputTokens += outTokens;
                            existing.RequestCount += reqs;
                        }
                        else if (inTokens > 0 || outTokens > 0 || reqs > 0)
                        {
                            report.DailyItems.Add(new OpenAiDailyUsageItem
                            {
                                Date = bucketDate,
                                ServiceOrModel = "Completions",
                                InputTokens = inTokens,
                                OutputTokens = outTokens,
                                RequestCount = reqs
                            });
                        }
                    }
                }
            }
        }
        catch { }
    }
}
