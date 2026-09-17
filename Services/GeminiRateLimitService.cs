namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;

public static class GeminiRateLimitService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<GeminiRateLimitReport> FetchRateLimitReportAsync(
        string apiKey,
        int days = 28,
        string projectName = "gen-lang-client-0458130432",
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        List<string> availableModels = [];

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            availableModels = await AiDataCacheService.GetOrFetchAsync<List<string>>(
                provider: "Gemini",
                cacheKey: "models_list",
                category: "models",
                ttl: AiDataCacheService.ModelsCacheTtl,
                isFinalized: false,
                fetcher: async () =>
                {
                    try
                    {
                        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey.Trim())}";
                        using var req = new HttpRequestMessage(HttpMethod.Get, url);
                        using var resp = await HttpClient.SendAsync(req, ct);

                        if (resp.IsSuccessStatusCode)
                        {
                            string body = await resp.Content.ReadAsStringAsync(ct);
                            return AiPriceCalculator.ParseGeminiModelsJson(body);
                        }
                    }
                    catch { }
                    return [];
                },
                forceRefresh: forceRefresh,
                ct: ct) ?? [];
        }

        // Fetch local SQLite AI call records for Google Gemini
        IReadOnlyList<AiUsageRecord> records = [];
        try
        {
            var repo = AiTokenUsageTrackerService.GetRepository();
            var since = DateTimeOffset.UtcNow.AddDays(-days);
            records = await repo.GetHistoryAsync(providerFilter: "Google Gemini", since: since, limit: 10000, cancellationToken: ct);
        }
        catch
        {
            // If repository query fails, empty records will be handled gracefully
        }

        return GeminiRateLimitReport.BuildFromHistory(availableModels, records, days, projectName);
    }
}
