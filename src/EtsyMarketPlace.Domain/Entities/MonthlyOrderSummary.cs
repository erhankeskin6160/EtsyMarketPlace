namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Mağaza bazında aylık sipariş ve ciro özetleri.
/// </summary>
public class MonthlyOrderSummary
{
    public string ShopId { get; set; } = string.Empty;
    public string YearMonth { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int UnitsSold { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal AvgOrderValue { get; set; }
    public int ShippedOrders { get; set; }
    public int UnfulfilledOrders { get; set; }
    public string Currency { get; set; } = "USD";

    public ShopConnection? Shop { get; set; }
}
