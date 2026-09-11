namespace EtsyMarketPlace.Domain.ShopVault.Entities;

public sealed class VaultListingVariation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long ListingId { get; set; }
    public long PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public long ValueId { get; set; }
    public string ValueName { get; set; } = string.Empty;
    public decimal PriceDifference { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int StockQuantity { get; set; } = 999;
    public bool IsAvailable { get; set; } = true;

    public VaultListingVariation() { }

    public VaultListingVariation(
        long listingId,
        long propertyId,
        string propertyName,
        long valueId,
        string valueName,
        decimal priceDifference = 0,
        string sku = "",
        int stockQuantity = 999,
        bool isAvailable = true)
    {
        ListingId = listingId;
        PropertyId = propertyId;
        PropertyName = propertyName;
        ValueId = valueId;
        ValueName = valueName;
        PriceDifference = priceDifference;
        Sku = sku;
        StockQuantity = stockQuantity;
        IsAvailable = isAvailable;
    }
}
