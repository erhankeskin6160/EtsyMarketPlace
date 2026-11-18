namespace EtsyMarketPlace.Domain.Subscriptions.Entities;

/// <summary>
/// Abonelik ödeme oturumları ve fatura işlem geçmişi kaydı.
/// </summary>
public class SubscriptionTransaction
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = "Monthly";
    public decimal AmountUsd { get; set; }
    public decimal AmountTry { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 35.0m;
    public string Status { get; set; } = "Pending";
    public string PaymentProvider { get; set; } = "StripeVirtualPos";
    public string? CardMaskedNumber { get; set; }
    public string? CardHolderName { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
}
