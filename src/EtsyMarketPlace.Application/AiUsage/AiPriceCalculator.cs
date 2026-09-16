namespace EtsyMarketPlace.Application.AiUsage;

using System;

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
}
