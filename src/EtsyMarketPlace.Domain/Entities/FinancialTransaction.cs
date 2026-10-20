namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Etsy ödeme defterinin (Payment Ledger) tekil finansal işlem satırı.
/// </summary>
public class FinancialTransaction
{
    public string ShopId { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public decimal GrossSales { get; set; }
    public decimal PlatformFees { get; set; }
    public decimal InternalAdsCost { get; set; }
    public decimal ExternalAdsCost { get; set; }
    public decimal ProductCost { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Refunds { get; set; }
    public string Currency { get; set; } = "USD";

    public ShopConnection? Shop { get; set; }
}
