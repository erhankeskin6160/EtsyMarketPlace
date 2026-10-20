namespace EtsyMarketPlace.Domain.Entities;

using System;

public class AiUsageRecordEntity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ModuleName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal CostUsd { get; set; }
    public decimal CostTry { get; set; }
    public string Status { get; set; } = "Başarılı";
    public string? Note { get; set; }
}
