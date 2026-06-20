namespace SimilarProductsWinForms.Models;

internal sealed class EtsyListingDto
{
    public long ListingId { get; init; }

    public string Title { get; init; } = "";

    public string ShopName { get; init; } = "";

    public string ShopUrl { get; init; } = "";

    public string ListingUrl { get; init; } = "";

    public string PriceDisplay { get; init; } = "";

    public string QuantityDisplay { get; init; } = "";

    public string SalesDisplay { get; init; } = "Urun bazli satis API sonucunda yok";
}
