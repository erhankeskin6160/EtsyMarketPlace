namespace EtsyMarketPlace.Domain.Subscriptions.Entities;

/// <summary>
/// SaaS abonelik paketi tanımı (Starter, Growth, Pro, Enterprise).
/// </summary>
public class SubscriptionPlan
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#38bdf8";
    public decimal MonthlyPriceUsd { get; set; }
    public decimal YearlyPriceUsd { get; set; }
    public decimal MonthlyPriceTry { get; set; }
    public decimal YearlyPriceTry { get; set; }
    public int MaxShops { get; set; } = 1;
    public int MonthlyAiTokenQuota { get; set; } = 100_000;
    /// <summary>Aylık AI ilan/denetim limiti (-1 = sınırsız).</summary>
    public int MaxMonthlyAiListings { get; set; } = 15;
    /// <summary>Toplu işlem kuyruğuna aynı anda eklenebilecek maksimum ürün sayısı (0 = kapalı).</summary>
    public int MaxBatchQueueSize { get; set; }
    public string AllowedFeaturesJson { get; set; } = "[]";
    public bool IsPopular { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
