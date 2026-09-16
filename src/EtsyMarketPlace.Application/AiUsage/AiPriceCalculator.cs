namespace EtsyMarketPlace.Application.AiUsage;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

public static class AiPriceCalculator
{
    public const decimal DefaultExchangeRateUsdTry = 40.0m;

    /// <summary>
    /// Model adı ve token sayısına göre tahmini USD maliyetini hesaplar.
    /// </summary>
    public static decimal CalculateCostUsd(string modelName, int promptTokens, int completionTokens)
    {
        var model = (modelName ?? "").ToLowerInvariant().Trim();

        // 1 Milyon token başına USD tarifeleri (Prompt / Completion)
        decimal promptRatePerMillion;
        decimal completionRatePerMillion;

        if (model.Contains("luna"))
        {
            // GPT-5.6 Luna: $0.20 / $1.20
            promptRatePerMillion = 0.20m;
            completionRatePerMillion = 1.20m;
        }
        else if (model.Contains("terra"))
        {
            // GPT-5.6 Terra: $2.00 / $12.00
            promptRatePerMillion = 2.00m;
            completionRatePerMillion = 12.00m;
        }
        else if (model.Contains("sol"))
        {
            // GPT-5.6 Sol: $5.00 / $30.00
            promptRatePerMillion = 5.00m;
            completionRatePerMillion = 30.00m;
        }
        else if (model.Contains("astra"))
        {
            // GPT-6 Astra: $10.00 / $50.00
            promptRatePerMillion = 10.00m;
            completionRatePerMillion = 50.00m;
        }
        else if (model.Contains("4o-mini"))
        {
            // GPT-4o-mini: $0.15 / $0.60
            promptRatePerMillion = 0.15m;
            completionRatePerMillion = 0.60m;
        }
        else if (model.Contains("gpt-4o") || model.Contains("gpt-4"))
        {
            // GPT-4o: $2.50 / $10.00
            promptRatePerMillion = 2.50m;
            completionRatePerMillion = 10.00m;
        }
        else if (model.Contains("deepseek-reasoner") || model.Contains("r1"))
        {
            // DeepSeek Reasoner (R1): $0.55 / $2.19
            promptRatePerMillion = 0.55m;
            completionRatePerMillion = 2.19m;
        }
        else if (model.Contains("deepseek") || model.Contains("v4") || model.Contains("v3"))
        {
            // DeepSeek Chat / V4: $0.15 / $0.60
            promptRatePerMillion = 0.15m;
            completionRatePerMillion = 0.60m;
        }
        else if (model.Contains("gemini-2.5-flash-lite") || model.Contains("flash-lite"))
        {
            // Gemini 2.5 Flash-Lite: $0.10 / $0.40
            promptRatePerMillion = 0.10m;
            completionRatePerMillion = 0.40m;
        }
        else if (model.Contains("gemini-2.5-flash") || model.Contains("gemini-3.8-flash") || model.Contains("gemini-3.6-flash"))
        {
            // Gemini 2.5 Flash: $0.30 / $2.50
            promptRatePerMillion = 0.30m;
            completionRatePerMillion = 2.50m;
        }
        else if (model.Contains("gemini"))
        {
            // Generic Gemini: $0.35 / $2.50
            promptRatePerMillion = 0.35m;
            completionRatePerMillion = 2.50m;
        }
        else if (model.Contains("claude-3-5-haiku") || model.Contains("haiku"))
        {
            // Claude Haiku: $0.80 / $4.00
            promptRatePerMillion = 0.80m;
            completionRatePerMillion = 4.00m;
        }
        else if (model.Contains("claude"))
        {
            // Claude Sonnet: $3.00 / $15.00
            promptRatePerMillion = 3.00m;
            completionRatePerMillion = 15.00m;
        }
        else if (model.Contains("grok"))
        {
            // xAI Grok: $2.00 / $10.00
            promptRatePerMillion = 2.00m;
            completionRatePerMillion = 10.00m;
        }
        else
        {
            // Genel varsayılan ortalama
            promptRatePerMillion = 0.30m;
            completionRatePerMillion = 1.50m;
        }

        decimal promptCost = (promptTokens / 1_000_000m) * promptRatePerMillion;
        decimal completionCost = (completionTokens / 1_000_000m) * completionRatePerMillion;

        return Math.Round(promptCost + completionCost, 6);
    }

    public static decimal CalculateCostTry(decimal costUsd, decimal exchangeRate = DefaultExchangeRateUsdTry)
    {
        return Math.Round(costUsd * exchangeRate, 4);
    }

    /// <summary>
    /// API anahtarını güvenli şekilde maskeler (örn: sk-proj-...8Abc)
    /// </summary>
    public static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "Tanımlanmadı";
        string trimmed = apiKey.Trim();
        if (trimmed.Length <= 8) return new string('*', trimmed.Length);

        if (trimmed.StartsWith("sk-proj-", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = trimmed.Length > 12 ? trimmed[^4..] : trimmed[^2..];
            return $"sk-proj-...{suffix}";
        }
        if (trimmed.StartsWith("sk-admin-", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = trimmed.Length > 13 ? trimmed[^4..] : trimmed[^2..];
            return $"sk-admin-...{suffix}";
        }
        if (trimmed.Length > 10)
        {
            return $"{trimmed[..4]}...{trimmed[^4..]}";
        }
        return $"{trimmed[..2]}...{trimmed[^2..]}";
    }

    /// <summary>
    /// OpenAI /v1/organization/costs JSON çıktısını ayrıştırır ve toplam harcanan doları hesaplar
    /// </summary>
    public static decimal ParseOpenAiCostsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0m;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            decimal totalCost = 0m;

            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataEl.EnumerateArray())
                {
                    if (item.TryGetProperty("results", out var resArr) && resArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var sub in resArr.EnumerateArray())
                        {
                            if (sub.TryGetProperty("amount", out var amtEl) && amtEl.TryGetProperty("value", out var valEl))
                            {
                                if (valEl.ValueKind == JsonValueKind.Number && valEl.TryGetDecimal(out var val))
                                    totalCost += val;
                                else if (valEl.ValueKind == JsonValueKind.String && decimal.TryParse(valEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                                    totalCost += parsed;
                            }
                        }
                    }
                    else if (item.TryGetProperty("amount", out var amtEl) && amtEl.TryGetProperty("value", out var valEl))
                    {
                        if (valEl.ValueKind == JsonValueKind.Number && valEl.TryGetDecimal(out var val))
                            totalCost += val;
                        else if (valEl.ValueKind == JsonValueKind.String && decimal.TryParse(valEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                            totalCost += parsed;
                    }
                }
            }
            else if (root.TryGetProperty("amount", out var singleAmt) && singleAmt.TryGetProperty("value", out var valProp))
            {
                if (valProp.ValueKind == JsonValueKind.Number && valProp.TryGetDecimal(out var val))
                    totalCost = val;
            }

            return Math.Round(totalCost, 4);
        }
        catch
        {
            return 0m;
        }
    }

    /// <summary>
    /// OpenAI /v1/organization/costs JSON çıktısını gün bazında detaylı liste olarak ayrıştırır.
    /// </summary>
    public static (List<OpenAiDailyUsageItem> Items, string? NextCursor) ParseOpenAiCostsDetailsJson(string json)
    {
        var items = new List<OpenAiDailyUsageItem>();
        string? nextCursor = null;
        if (string.IsNullOrWhiteSpace(json)) return (items, nextCursor);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("next_page", out var npEl) && npEl.ValueKind == JsonValueKind.String)
            {
                nextCursor = npEl.GetString();
            }

            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var bucket in dataEl.EnumerateArray())
                {
                    DateTime bucketDate = DateTime.UtcNow.Date;
                    if (bucket.TryGetProperty("start_time", out var stEl) && stEl.TryGetInt64(out long st))
                    {
                        bucketDate = DateTimeOffset.FromUnixTimeSeconds(st).UtcDateTime.Date;
                    }
                    else if (bucket.TryGetProperty("timestamp", out var tsEl) && tsEl.TryGetInt64(out long ts))
                    {
                        bucketDate = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.Date;
                    }

                    if (bucket.TryGetProperty("results", out var resArr) && resArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var sub in resArr.EnumerateArray())
                        {
                            var daily = new OpenAiDailyUsageItem { Date = bucketDate };
                            if (sub.TryGetProperty("line_item", out var lineEl))
                                daily.ServiceOrModel = lineEl.GetString() ?? "Genel";

                            if (sub.TryGetProperty("amount", out var amtEl) && amtEl.TryGetProperty("value", out var valEl))
                            {
                                if (valEl.ValueKind == JsonValueKind.Number && valEl.TryGetDecimal(out var val))
                                    daily.CostUsd = val;
                                else if (valEl.ValueKind == JsonValueKind.String && decimal.TryParse(valEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dVal))
                                    daily.CostUsd = dVal;
                            }
                            items.Add(daily);
                        }
                    }
                    else if (bucket.TryGetProperty("amount", out var amtEl) && amtEl.TryGetProperty("value", out var valEl))
                    {
                        var daily = new OpenAiDailyUsageItem { Date = bucketDate };
                        if (bucket.TryGetProperty("line_item", out var lineEl))
                            daily.ServiceOrModel = lineEl.GetString() ?? "Genel";

                        if (valEl.ValueKind == JsonValueKind.Number && valEl.TryGetDecimal(out var val))
                            daily.CostUsd = val;
                        else if (valEl.ValueKind == JsonValueKind.String && decimal.TryParse(valEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var dVal))
                            daily.CostUsd = dVal;

                        items.Add(daily);
                    }
                }
            }
        }
        catch { }

        return (items, nextCursor);
    }

    /// <summary>
    /// OpenAI /v1/organization/usage/completions (veya embeddings) JSON çıktısını rapora birleştirir.
    /// Girdi token, çıktı token ve istek sayılarını gün ve model bazında eşleştirir.
    /// </summary>
    public static string? MergeOpenAiUsageJson(OpenAiOfficialUsageReport report, string json, string defaultModel = "gpt-4o")
    {
        if (string.IsNullOrWhiteSpace(json) || report == null) return null;
        string? nextCursor = null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("next_page", out var npEl) && npEl.ValueKind == JsonValueKind.String)
            {
                nextCursor = npEl.GetString();
            }

            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var bucket in dataEl.EnumerateArray())
                {
                    DateTime bucketDate = DateTime.UtcNow.Date;
                    if (bucket.TryGetProperty("start_time", out var stEl) && stEl.TryGetInt64(out long st))
                    {
                        bucketDate = DateTimeOffset.FromUnixTimeSeconds(st).UtcDateTime.Date;
                    }
                    else if (bucket.TryGetProperty("timestamp", out var tsEl) && tsEl.TryGetInt64(out long ts))
                    {
                        bucketDate = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.Date;
                    }

                    if (bucket.TryGetProperty("results", out var resArr) && resArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var r in resArr.EnumerateArray())
                        {
                            long inTokens = r.TryGetProperty("input_tokens", out var inEl) ? inEl.GetInt64() : 0;
                            long outTokens = r.TryGetProperty("output_tokens", out var outEl) ? outEl.GetInt64() : 0;
                            int reqs = r.TryGetProperty("num_model_requests", out var reqEl) ? reqEl.GetInt32() : 0;
                            string? modelProp = r.TryGetProperty("model", out var mEl) ? mEl.GetString() : null;
                            string model = !string.IsNullOrWhiteSpace(modelProp) ? modelProp : defaultModel;

                            if (inTokens > 0 || outTokens > 0 || reqs > 0)
                            {
                                var existing = report.DailyItems.FirstOrDefault(x => x.Date.Date == bucketDate.Date && (x.ServiceOrModel == model || x.ServiceOrModel == "Genel" || string.IsNullOrWhiteSpace(x.ServiceOrModel)));
                                if (existing != null)
                                {
                                    if (existing.ServiceOrModel == "Genel" && !string.IsNullOrWhiteSpace(model))
                                        existing.ServiceOrModel = model;
                                    existing.InputTokens += inTokens;
                                    existing.OutputTokens += outTokens;
                                    existing.RequestCount += reqs;
                                }
                                else
                                {
                                    report.DailyItems.Add(new OpenAiDailyUsageItem
                                    {
                                        Date = bucketDate,
                                        ServiceOrModel = model,
                                        InputTokens = inTokens,
                                        OutputTokens = outTokens,
                                        RequestCount = reqs
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback flat properties
                        long inTokens = bucket.TryGetProperty("input_tokens", out var inEl) ? inEl.GetInt64() : 0;
                        long outTokens = bucket.TryGetProperty("output_tokens", out var outEl) ? outEl.GetInt64() : 0;
                        int reqs = bucket.TryGetProperty("num_model_requests", out var reqEl) ? reqEl.GetInt32() : 0;

                        if (inTokens > 0 || outTokens > 0 || reqs > 0)
                        {
                            var existing = report.DailyItems.FirstOrDefault(x => x.Date.Date == bucketDate.Date);
                            if (existing != null)
                            {
                                existing.InputTokens += inTokens;
                                existing.OutputTokens += outTokens;
                                existing.RequestCount += reqs;
                            }
                            else
                            {
                                report.DailyItems.Add(new OpenAiDailyUsageItem
                                {
                                    Date = bucketDate,
                                    ServiceOrModel = defaultModel,
                                    InputTokens = inTokens,
                                    OutputTokens = outTokens,
                                    RequestCount = reqs
                                });
                            }
                        }
                    }
                }
            }

            // Toplamları eşitle
            report.TotalTokens = report.DailyItems.Sum(x => x.TotalTokens);
            report.TotalRequests = report.DailyItems.Sum(x => x.RequestCount);
            report.TotalCostUsd = report.DailyItems.Sum(x => x.CostUsd);
        }
        catch { }

        return nextCursor;
    }
}
