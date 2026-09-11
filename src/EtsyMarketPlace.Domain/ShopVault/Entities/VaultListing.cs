namespace EtsyMarketPlace.Domain.ShopVault.Entities;

public sealed class VaultListing
{
    public long ListingId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public long OriginalShopId { get; set; }
    public string OriginalShopName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public int Quantity { get; set; } = 999;

    public List<string> Tags { get; set; } = [];
    public List<string> Materials { get; set; } = [];

    public long TaxonomyId { get; set; }
    public string TaxonomyPath { get; set; } = string.Empty;

    public string WhoMade { get; set; } = "i_did";
    public string WhenMade { get; set; } = "made_to_order";
    public bool IsSupply { get; set; }
    public bool IsDigital { get; set; }

    public long? ShippingProfileId { get; set; }
    public string ShippingProfileTitle { get; set; } = string.Empty;
    public long? ReturnPolicyId { get; set; }

    public string State { get; set; } = "active";
    public string OriginalListingUrl { get; set; } = string.Empty;
    public DateTime BackedUpAtUtc { get; set; } = DateTime.UtcNow;

    public List<VaultListingImage> Images { get; set; } = [];
    public List<VaultListingVariation> Variations { get; set; } = [];

    public VaultListing() { }

    public VaultListing(
        long listingId,
        string sessionId,
        long originalShopId,
        string originalShopName,
        string title,
        string description,
        decimal price,
        string currency = "USD",
        int quantity = 999)
    {
        ListingId = listingId;
        SessionId = sessionId;
        OriginalShopId = originalShopId;
        OriginalShopName = originalShopName;
        Title = title;
        Description = description;
        Price = price;
        Currency = currency;
        Quantity = quantity;
        BackedUpAtUtc = DateTime.UtcNow;
    }

    public string PrimaryThumbnailUrl => Images.Count > 0
        ? (Images.OrderBy(i => i.Rank).FirstOrDefault()?.OriginalUrl ?? string.Empty)
        : string.Empty;

    public string PrimaryThumbnailLocalPath => Images.Count > 0
        ? (Images.OrderBy(i => i.Rank).FirstOrDefault()?.LocalRelativePath ?? string.Empty)
        : string.Empty;
}
