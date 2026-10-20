namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Günlük ürün listeleme trafiği, favori ve dönüşüm oranı verileri.
/// </summary>
public class ListingTrafficDaily
{
    public string ShopId { get; set; } = string.Empty;
    public long ListingId { get; set; }
    public string SnapshotDate { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Views { get; set; }
    public int Favorites { get; set; }
    public int ViewsToday { get; set; }
    public int FavoritesToday { get; set; }
    public int UnitsSoldMonth { get; set; }
    public decimal RevenueMonth { get; set; }
    public decimal ConversionRate { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ListingUrl { get; set; } = string.Empty;

    public ShopConnection? Shop { get; set; }
}
