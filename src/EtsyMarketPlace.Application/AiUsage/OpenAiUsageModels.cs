namespace EtsyMarketPlace.Application.AiUsage;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class OpenAiDailyUsageItem
{
    public DateTime Date { get; set; }
    public string ServiceOrModel { get; set; } = "Genel";
    public int RequestCount { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TotalTokens => InputTokens + OutputTokens;
    public decimal CostUsd { get; set; }
    public decimal CostTry => Math.Round(CostUsd * 40.0m, 2);
}

public sealed class OpenAiOfficialUsageReport
{
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalCostUsd { get; set; }
    public decimal TotalCostTry => Math.Round(TotalCostUsd * 40.0m, 2);
    public long TotalTokens { get; set; }
    public long TotalInputTokens => DailyItems.Sum(x => x.InputTokens);
    public long TotalOutputTokens => DailyItems.Sum(x => x.OutputTokens);
    public int TotalRequests { get; set; }
    public bool HasAdminKey { get; set; }
    public string MaskedKey { get; set; } = string.Empty;
    public string DataSource { get; set; } = "OpenAI API";
    public string? ErrorMessage { get; set; }
    public List<OpenAiDailyUsageItem> DailyItems { get; set; } = [];
}
