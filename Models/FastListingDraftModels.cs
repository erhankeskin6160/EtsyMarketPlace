namespace SimilarProductsWinForms.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Hızlı Ürün Ekle (AI) modülünde kullanıcının girdiği verilerin
/// modüller arası geçişlerde veya uygulama kapanmalarında kaybolmasını önleyen taslak veri modeli.
/// </summary>
internal sealed class FastListingDraftModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string ProductType { get; set; } = "Fiziksel";
    public string Price { get; set; } = "29.99";
    public decimal Quantity { get; set; } = 10;
    
    public string? SelectedTaxonomyText { get; set; }
    public long? SelectedTaxonomyId { get; set; }
    
    public long? SelectedShippingProfileId { get; set; }
    public long? SelectedReadinessStateId { get; set; }
    
    public List<string> GalleryImagePaths { get; set; } = new();
    
    public bool HasVariations { get; set; } = false;
    public string VariationType { get; set; } = "Boyut / Size";
    public string VariationOptionsText { get; set; } = "Small, Medium, Large";
    public bool CustomPricePerVariation { get; set; } = false;
    
    public bool PublishDirectly { get; set; } = false;
    public DateTimeOffset LastSavedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title) &&
        string.IsNullOrWhiteSpace(Description) &&
        string.IsNullOrWhiteSpace(Tags) &&
        (GalleryImagePaths == null || GalleryImagePaths.Count == 0);
}
