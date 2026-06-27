namespace SimilarProductsWinForms.Models;

internal sealed class MarketListingResult
{
    public int ListingRank { get; set; }
    public long ListingId { get; init; }
    public long ShopId { get; init; }
    public long TaxonomyId { get; init; }
    public string TaxonomyName { get; set; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string ListingUrl { get; init; } = "";
    public string ImageUrl { get; init; } = "";
    public string ShopName { get; set; } = "";
    public string ShopUrl { get; set; } = "";
    public decimal Price { get; init; }
    public string CurrencyCode { get; init; } = "USD";
    public int Quantity { get; init; }
    public int Favorites { get; init; }
    public int Views { get; init; }
    public int ShopSales { get; set; }
    public int ReviewCount { get; set; }
    public decimal ReviewAverage { get; set; }
    public List<string> Tags { get; init; } = [];
    public List<string> ImageUrls { get; set; } = [];
    public System.Drawing.Image? ThumbnailImage { get; set; }
    public int SeoScore { get; init; }
    public int MarketScore { get; set; }

    public string PriceDisplay => $"{CurrencyCode} {Price:0.##}";
    public string TagsDisplay => string.Join(", ", Tags);
    public string ProductSalesDisplay => "Etsy API urun bazli satis adedi sunmuyor";
    public string ShopSalesDisplay => ShopSales > 0 ? ShopSales.ToString("N0") : "Veri yok";
    public string ViewsDisplay => Views > 0 ? Views.ToString("N0") : "Veri yok";
    public string ImageCountDisplay => ImageUrls.Count > 0 ? $"{ImageUrls.Count} resim" : "Resim yok";
    public string TaxonomyDisplay => !string.IsNullOrWhiteSpace(TaxonomyName)
        ? TaxonomyName
        : TaxonomyId > 0 ? $"Kategori bulunamadi (#{TaxonomyId})" : "Kategori verisi yok";
}
