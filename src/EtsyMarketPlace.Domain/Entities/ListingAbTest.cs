namespace EtsyMarketPlace.Domain.Entities;

using System;

public class ListingAbTest
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ListingId { get; set; } = string.Empty;
    public string ListingTitle { get; set; } = string.Empty;
    public string ExperimentName { get; set; } = string.Empty;
    public string VariantATitle { get; set; } = string.Empty;
    public string VariantBTitle { get; set; } = string.Empty;
    public string VariantATags { get; set; } = "[]";
    public string VariantBTags { get; set; } = "[]";
    public string VariantADescription { get; set; } = string.Empty;
    public string VariantBDescription { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Active";
    public int BeforeViews { get; set; }
    public int BeforeFavorites { get; set; }
    public int BeforeSales { get; set; }
    public int AfterViews { get; set; }
    public int AfterFavorites { get; set; }
    public int AfterSales { get; set; }
}
