namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Etsy ürün URL'leri, mağaza taslak linkleri ve direkt listing kimliklerinden
/// geçerli listing ID çözümleme ve standart URL oluşturma motoru.
/// </summary>
public static class EtsyListingUrlParser
{
    public static bool TryExtractListingId(string? value, out long listingId)
    {
        listingId = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        var match = Regex.Match(trimmed, @"/listing/(?<id>\d{6,})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(trimmed, @"(?:listing|copy)/(?<id>\d{6,})", RegexOptions.IgnoreCase);
        }
        if (!match.Success)
        {
            match = Regex.Match(trimmed, @"listing_id=(?<id>\d{6,})", RegexOptions.IgnoreCase);
        }
        if (!match.Success)
        {
            match = Regex.Match(trimmed, @"(?<id>\d{8,})");
        }

        return match.Success && long.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out listingId);
    }

    public static string BuildListingUrl(long listingId) => $"https://www.etsy.com/listing/{listingId}";
}
