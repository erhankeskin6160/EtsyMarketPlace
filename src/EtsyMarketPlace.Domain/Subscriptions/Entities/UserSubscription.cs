namespace EtsyMarketPlace.Domain.Subscriptions.Entities;

/// <summary>
/// Kullanıcının aktif veya geçmiş SaaS abonelik kaydı.
/// </summary>
public class UserSubscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string PlanId { get; set; } = "pro";
    public string Status { get; set; } = "Active";
    public string BillingCycle { get; set; } = "Monthly";
    public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodStartUtc { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEndUtc { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool AutoRenew { get; set; } = true;
    public int? CustomMaxShopsOverride { get; set; }
    public int? CustomAiTokenQuotaOverride { get; set; }
    public string PaymentProvider { get; set; } = "StripeVirtualPos";
    public string? LastSessionId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
