namespace EtsyMarketPlace.Application.ListingOptimization;

using System;

public sealed class FastListingTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string CategoryTaxonomyId { get; set; } = string.Empty;
    public string CategoryTaxonomyName { get; set; } = string.Empty;
    public string ShippingProfileId { get; set; } = string.Empty;
    public string ShippingProfileName { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; } = 24.99m;
    public int DefaultQuantity { get; set; } = 10;
    public bool IsDigital { get; set; } = false;
    public string DefaultTags { get; set; } = string.Empty;
    public string DefaultMaterials { get; set; } = string.Empty;
    public string DescriptionTemplate { get; set; } = string.Empty;
    public bool EnableVariations { get; set; } = false;
    public string VariationType1 { get; set; } = "Boyut / Beden (Size - 100)";
    public string VariationValues1 { get; set; } = string.Empty;
    public bool EnableVariation2 { get; set; } = false;
    public string VariationType2 { get; set; } = "Renk (Primary Color - 506)";
    public string VariationValues2 { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public override string ToString() => Name;
}
