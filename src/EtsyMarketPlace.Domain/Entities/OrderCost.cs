namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Sipariş bazında ürün ve kargo maliyeti ile kâr marjı takibi.
/// </summary>
public class OrderCost
{
    public string ShopId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal OrderTotal { get; set; }
    public decimal? ProductCost { get; set; }
    public decimal? ShippingCost { get; set; }
    public string? AlertReason { get; set; }

    public ShopConnection? Shop { get; set; }
}
