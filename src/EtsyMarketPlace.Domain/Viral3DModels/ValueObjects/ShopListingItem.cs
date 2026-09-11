namespace EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

using System.Collections.Generic;

public sealed class ShopListingItem
{
    public long ListingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Category { get; set; } = string.Empty;
    public List<string> Materials { get; set; } = [];
}
