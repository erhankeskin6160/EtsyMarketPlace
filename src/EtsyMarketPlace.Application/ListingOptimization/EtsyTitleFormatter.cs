namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Text.RegularExpressions;

/// <summary>
/// Normalizes and sanitizes Etsy listing titles to strictly comply with Etsy API rules.
/// Specifically enforces:
/// - NO ampersand (&) characters: replaces '&' with 'and' (Etsy API restricts '&' and rejects listings with multiple ampersands)
/// - Maximum 140 characters limit
/// - Clean whitespace and formatting
/// </summary>
public static class EtsyTitleFormatter
{
    private static readonly Regex AmpersandRegex = new(@"\s*&\s*", RegexOptions.Compiled);
    private static readonly Regex MultipleSpacesRegex = new(@"\s{2,}", RegexOptions.Compiled);
    private static readonly Regex UiHeaderRegex = new(@"\[(AI İLE OPTİMİZE EDİLMİŞ BAŞLIK|MEVCUT BAŞLIK)[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ActiveEngineRegex = new(@"\s*\(Aktif Motor:[^\)]*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Normalizes a single Etsy listing title.
    /// Replaces all ampersands (&) with 'and', strips UI labels, trims whitespace,
    /// and ensures maximum length of 140 characters.
    /// </summary>
    public static string NormalizeForEtsy(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle)) return "";

        var cleaned = rawTitle.Trim();

        // 1. Çoklu satır varsa (UI'dan başlık kopyalandığında), başlık etiketini ve aktif motor bilgisini temizle
        var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count > 1)
        {
            var contentLines = lines.Where(l =>
                !l.StartsWith("[", StringComparison.OrdinalIgnoreCase) &&
                !l.Contains("Aktif Motor:", StringComparison.OrdinalIgnoreCase) &&
                !l.Contains("BAŞLIK", StringComparison.OrdinalIgnoreCase)).ToList();

            if (contentLines.Count > 0)
            {
                cleaned = contentLines[0];
            }
        }

        cleaned = UiHeaderRegex.Replace(cleaned, "");
        cleaned = Regex.Replace(cleaned, @"\s*\(Aktif Motor:[^\n\r]*\)", "", RegexOptions.IgnoreCase);

        // 2. Replace any ampersand (&) with " and " (Etsy API restricts '&' and rejects listings with multiple '&')
        cleaned = AmpersandRegex.Replace(cleaned, " and ");

        // 3. Normalize multiple spaces
        cleaned = MultipleSpacesRegex.Replace(cleaned, " ").Trim();

        // 4. Ensure character limit (max 140 characters)
        if (cleaned.Length > 140)
        {
            cleaned = cleaned[..140].TrimEnd();
            int lastSpace = cleaned.LastIndexOf(' ');
            if (lastSpace > 115)
            {
                cleaned = cleaned[..lastSpace].TrimEnd();
            }
        }

        return cleaned;
    }
}
