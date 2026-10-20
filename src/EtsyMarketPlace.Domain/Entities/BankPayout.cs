namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Etsy'nin satıcının banka hesabına gönderdiği hakediş transferi.
/// </summary>
public class BankPayout
{
    public string ShopId { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? ExchangeRateToTry { get; set; }
    public string Status { get; set; } = "PAID";
    public string Description { get; set; } = string.Empty;

    public ShopConnection? Shop { get; set; }
}
