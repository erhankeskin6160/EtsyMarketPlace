namespace EtsyMarketPlace.Application.AiUsage;

using System;
using System.Collections.Generic;

public sealed class AiUsageRecord
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string ModuleName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public decimal EstimatedCostTry { get; set; }
    public string Status { get; set; } = "Başarılı";
    public string? Note { get; set; }
}

public sealed class AiProviderBalanceInfo
{
    public string Provider { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public decimal TotalBalanceUsd { get; set; }
    public decimal GrantedBalanceUsd { get; set; }
    public decimal ToppedUpBalanceUsd { get; set; }
    public string Currency { get; set; } = "USD";
    public string StatusMessage { get; set; } = string.Empty;
    public int DailyRequestsUsed { get; set; }
    public int DailyQuotaLimit { get; set; }
    public string MaskedApiKey { get; set; } = string.Empty;
    public decimal? OfficialMonthlyCostUsd { get; set; }
    public bool HasAdminKey { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.Now;

    public decimal TotalBalanceTry(decimal exchangeRate = 40.0m) =>
        Math.Round(TotalBalanceUsd * exchangeRate, 2);
}

public sealed class AiUsageSummaryStats
{
    public string ProviderFilter { get; set; } = "Tümü";
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int Blocked429Requests { get; set; }
    public int ErrorRequests { get; set; }
    public long TotalPromptTokens { get; set; }
    public long TotalCompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public decimal TotalCostTry { get; set; }
    public Dictionary<string, decimal> CostByModel { get; set; } = [];
    public Dictionary<string, long> TokensByProvider { get; set; } = [];
}
