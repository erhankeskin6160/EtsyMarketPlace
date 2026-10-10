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
    /// In Etsy Open API v3, custom seller-defined variations use reserved property IDs:
    /// Group 1 = 513, Group 2 = 514. Legacy property ID 100 is deprecated by Etsy.
    /// </summary>
    public static (string Name, long PropertyId) ParseVariationType(string? selected, int groupIndex = 1)
    {
        long targetPropertyId = groupIndex == 2 ? 514 : 513;

        if (string.IsNullOrWhiteSpace(selected))
        {
            return ("Custom", targetPropertyId);
        }

        var lower = selected.ToLowerInvariant();
        if (lower.Contains("boyut") || lower.Contains("size"))
        {
            return ("Size", targetPropertyId);
        }

        if (lower.Contains("renk") || lower.Contains("color"))
        {
            return ("Color", targetPropertyId);
        }

        if (lower.Contains("malzeme") || lower.Contains("material"))
        {
            return ("Material", targetPropertyId);
        }

        if (lower.Contains("stil") || lower.Contains("style"))
        {
            return ("Style", targetPropertyId);
        }

        return ("Custom", targetPropertyId);
    }

    public const int MaxMaterialsCount = 13;
    public const int MaxMaterialCharLength = 45;

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

        var split = text.Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries);
        return SanitizeTags(split, maxTags, maxCharsPerTag);
    }

    /// <summary>
    /// Sanitizes an enumerable of tag strings for Etsy compliance (max 20 chars, max 13 tags, valid characters).
    /// </summary>
    public static List<string> SanitizeTags(
        IEnumerable<string>? tags,
        int maxTags = MaxTagsCount,
        int maxCharsPerTag = MaxTagCharLength)
    {
        if (tags == null)
        {
            return [];
        }

        return tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => SanitizeSingleTag(t, maxCharsPerTag))
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxTags)
            .ToList();
    }

    /// <summary>
    /// Sanitizes a single tag for Etsy API compliance (letters, digits, spaces, hyphens, max 20 chars).
    /// </summary>
    public static string SanitizeSingleTag(string? tag, int maxChars = MaxTagCharLength)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        var mapped = tag.Trim()
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Replace('ş', 's')
            .Replace('Ş', 'S')
            .Replace('ç', 'c')
            .Replace('Ç', 'C')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'G')
            .Replace('ü', 'u')
            .Replace('Ü', 'U')
            .Replace('ö', 'o')
            .Replace('Ö', 'O')
            .Replace('&', ' ')
            .Replace('/', ' ')
            .Replace('\\', ' ')
            .Replace('|', ' ')
            .Replace('+', ' ')
            .Replace('–', '-')
            .Replace('—', '-');

        var normalized = mapped.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-')
            {
                builder.Append(ch);
            }
        }

        var collapsed = string.Join(
            ' ',
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim('-', ' ');

        return collapsed.Length <= maxChars
            ? collapsed
            : collapsed[..maxChars].TrimEnd('-', ' ');
    }

    /// <summary>
    /// Sanitizes an enumerable of material strings for strict Etsy API v3 compliance.
    /// Etsy strictly requires: only letters, digits, spaces, and hyphens [a-zA-Z0-9 -].
    /// Any special characters (&amp;, /, \, |, (), [], etc.) cause HTTP 400 invalid_characters.
    /// Converts Turkish characters, strips combining marks, collapses whitespace,
    /// caps each material at 45 characters, removes duplicates, and takes maximum 13 items.
    /// </summary>
    public static List<string> SanitizeMaterials(
        IEnumerable<string>? materials,
        int maxMaterials = MaxMaterialsCount,
        int maxCharsPerMaterial = MaxMaterialCharLength)
    {
        if (materials == null)
        {
            return [];
        }

        return materials
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => SanitizeSingleMaterial(m, maxCharsPerMaterial))
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxMaterials)
            .ToList();
    }

    /// <summary>
    /// Sanitizes a comma- or newline-delimited material text string into Etsy-compliant materials list.
    /// </summary>
    public static List<string> SanitizeMaterials(
        string? text,
        int maxMaterials = MaxMaterialsCount,
        int maxCharsPerMaterial = MaxMaterialCharLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var split = text.Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries);
        return SanitizeMaterials(split, maxMaterials, maxCharsPerMaterial);
    }

    /// <summary>
    /// Sanitizes a single material string according to strict Etsy Open API v3 character constraints.
    /// </summary>
    public static string SanitizeSingleMaterial(string? material, int maxChars = MaxMaterialCharLength)
    {
        if (string.IsNullOrWhiteSpace(material))
        {
            return string.Empty;
        }

        // 1. Explicit Turkish character transliteration to ASCII
        var mapped = material.Trim()
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Replace('ş', 's')
            .Replace('Ş', 'S')
            .Replace('ç', 'c')
            .Replace('Ç', 'C')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'G')
            .Replace('ü', 'u')
            .Replace('Ü', 'U')
            .Replace('ö', 'o')
            .Replace('Ö', 'O');

        // 2. Pre-replace common separators and symbols
        mapped = mapped
            .Replace('&', ' ')
            .Replace('/', ' ')
            .Replace('\\', ' ')
            .Replace('|', ' ')
            .Replace('+', ' ')
            .Replace('_', ' ')
            .Replace('(', ' ')
            .Replace(')', ' ')
            .Replace('[', ' ')
            .Replace(']', ' ')
            .Replace('{', ' ')
            .Replace('}', ' ')
            .Replace('*', ' ')
            .Replace('•', ' ')
            .Replace('"', ' ')
            .Replace('\'', ' ')
            .Replace('`', ' ')
            .Replace(':', ' ')
            .Replace(';', ' ')
            .Replace('.', ' ')
            .Replace('!', ' ')
            .Replace('?', ' ')
            .Replace('–', '-')
            .Replace('—', '-');

        // 3. Normalize FormD to decompose any other accented characters
        var normalized = mapped.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Etsy allows ONLY letters, digits, spaces, and hyphens
            if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-')
            {
                builder.Append(ch);
            }
        }

        // 4. Collapse multiple spaces and trim edge hyphens/spaces
        var collapsed = string.Join(
            ' ',
            builder
                .ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim('-', ' ');

        return collapsed.Length <= maxChars
            ? collapsed
            : collapsed[..maxChars].TrimEnd('-', ' ');
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
