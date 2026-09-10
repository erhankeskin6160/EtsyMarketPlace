namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Domain and application helper utilities for fast listing draft creation,
/// variation group mapping, tag sanitation, and listing pre-publish validation.
/// </summary>
public static class FastListingDraftHelper
{
    public const int MaxTitleLength = 140;
    public const int MaxTagsCount = 13;
    public const int MaxTagCharLength = 20;
    public const int MaxVariationCombinations = 70;

    /// <summary>
    /// Parses variation type dropdown selection or text into Etsy canonical Property Name and Property ID.
    /// Standard Etsy property IDs:
    /// Size = 100, Primary Color = 506, Material = 507, Style = 514, Custom = 513.
    /// </summary>
    public static (string Name, long PropertyId) ParseVariationType(string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected))
        {
            return ("Custom", 513);
        }

        var lower = selected.ToLowerInvariant();
        if (lower.Contains("boyut") || lower.Contains("size") || lower.Contains("100"))
        {
            return ("Size", 100);
        }

        if (lower.Contains("renk") || lower.Contains("color") || lower.Contains("506"))
        {
            return ("Primary color", 506);
        }

        if (lower.Contains("malzeme") || lower.Contains("material") || lower.Contains("507"))
        {
            return ("Material", 507);
        }

        if (lower.Contains("stil") || lower.Contains("style") || lower.Contains("514"))
        {
            return ("Style", 514);
        }

        return ("Custom", 513);
    }

    /// <summary>
    /// Splits raw tag string by commas and newlines, strips whitespace, removes duplicates,
    /// enforces maximum 20 characters per tag, and limits the list to max 13 tags.
    /// </summary>
    public static List<string> SanitizeTags(string? text, int maxTags = MaxTagsCount, int maxCharsPerTag = MaxTagCharLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Length > maxCharsPerTag ? t[..maxCharsPerTag].Trim() : t)
            .Where(t => t.Length > 0)
            .Take(maxTags)
            .ToList();
    }

    /// <summary>
    /// Validates core listing fields before draft creation and returns a list of error messages (if any).
    /// </summary>
    public static List<string> ValidateListing(
        string? title,
        decimal price,
        int quantity,
        IReadOnlyList<string>? imagePaths)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add("Ürün başlığı zorunludur.");
        }
        else if (title.Trim().Length > MaxTitleLength)
        {
            errors.Add($"Ürün başlığı en fazla {MaxTitleLength} karakter olabilir (Şu anki: {title.Trim().Length}).");
        }

        if (price <= 0)
        {
            errors.Add("Fiyat 0'dan büyük bir değer olmalıdır.");
        }

        if (quantity <= 0)
        {
            errors.Add("Stok adedi en az 1 olmalıdır.");
        }

        if (imagePaths == null || imagePaths.Count == 0)
        {
            errors.Add("En az 1 adet ürün görseli eklenmelidir.");
        }

        return errors;
    }

    /// <summary>
    /// Calculates the number of variation combinations from 1 or 2 variation groups
    /// and checks if it exceeds Etsy's maximum allowed offerings limit (70).
    /// </summary>
    public static (int CombinationCount, bool ExceedsLimit) CalculateVariationCombinations(
        int group1Count,
        int group2Count = 0)
    {
        if (group1Count <= 0 && group2Count <= 0)
        {
            return (0, false);
        }

        var count1 = Math.Max(1, group1Count);
        var count2 = Math.Max(1, group2Count);
        var total = count1 * count2;

        return (total, total > MaxVariationCombinations);
    }
}
