namespace SimilarProductsWinForms.Services;

using System.Text.RegularExpressions;
using SimilarProductsWinForms.Models;

internal static partial class CompetitorShopAnalyzer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "the", "for", "with", "from", "your", "you", "this", "that", "a", "an", "of", "to", "in", "on", "or",
        "ve", "ile", "icin", "bir", "bu", "da", "de", "veya",
    };

    public static CompetitorShopAnalysis Analyze(CompetitorShopProfile shop, List<MarketListingResult> listings)
    {
        var dominantCurrency = listings
            .Where(item => item.Price > 0 && !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .GroupBy(item => item.CurrencyCode)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? "";
        var priced = listings
            .Where(item => item.Price > 0 && item.CurrencyCode.Equals(dominantCurrency, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Price)
            .Order()
            .ToList();
        var currencies = listings
            .Where(item => !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .GroupBy(item => item.CurrencyCode)
            .OrderByDescending(group => group.Count())
            .Select(group => $"{group.Key} ({group.Count()})")
            .ToList();

        return new CompetitorShopAnalysis
        {
            Shop = shop,
            Listings = listings,
            TopTags = BuildFrequency(listings.SelectMany(item => item.Tags), listings.Count, 25),
            TopTitleTerms = BuildFrequency(
                listings.SelectMany(item => Tokenize(item.Title).Distinct(StringComparer.OrdinalIgnoreCase)),
                listings.Count,
                25),
            TaxonomyDistribution = BuildFrequency(
                listings.Where(item => item.TaxonomyId > 0).Select(item => $"Taksonomi #{item.TaxonomyId}"),
                listings.Count,
                20),
            MinimumPrice = priced.Count > 0 ? priced[0] : 0,
            MaximumPrice = priced.Count > 0 ? priced[^1] : 0,
            AveragePrice = priced.Count > 0 ? priced.Average() : 0,
            MedianPrice = Median(priced),
            AverageFavorites = listings.Count > 0 ? (decimal)listings.Average(item => item.Favorites) : 0,
            AverageViews = listings.Count > 0 ? (decimal)listings.Average(item => item.Views) : 0,
            AverageSeoScore = listings.Count > 0 ? (int)Math.Round(listings.Average(item => item.SeoScore)) : 0,
            CompetitorStrengthScore = CalculateStrength(shop, listings),
            RetrievedAt = DateTimeOffset.Now,
            CurrencyDisplay = currencies.Count > 0 ? string.Join(", ", currencies) : "Para birimi yok",
            PriceCurrency = dominantCurrency,
        };
    }

    private static List<FrequencyMetric> BuildFrequency(IEnumerable<string> values, int listingCount, int limit)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 1)
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FrequencyMetric(
                group.Key,
                group.Count(),
                listingCount > 0 ? Math.Round(group.Count() * 100m / listingCount, 1) : 0))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static IEnumerable<string> Tokenize(string title)
    {
        return WordRegex().Matches(title.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length > 2 && !StopWords.Contains(word));
    }

    private static decimal Median(List<decimal> sorted)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2m : sorted[middle];
    }

    private static int CalculateStrength(CompetitorShopProfile shop, List<MarketListingResult> listings)
    {
        if (listings.Count == 0)
        {
            return 0;
        }

        var sales = Math.Min(25m, (decimal)Math.Log10(Math.Max(1, shop.TotalSales) + 1) * 6m);
        var reviews = Math.Min(15m, shop.ReviewAverage * 2m + (decimal)Math.Log10(Math.Max(1, shop.ReviewCount) + 1) * 2m);
        var favorites = Math.Min(20m, (decimal)Math.Log10(Math.Max(1, listings.Average(item => item.Favorites)) + 1) * 8m);
        var views = Math.Min(15m, (decimal)Math.Log10(Math.Max(1, listings.Average(item => item.Views)) + 1) * 5m);
        var seo = (decimal)listings.Average(item => item.SeoScore) * 0.15m;
        var variety = Math.Min(10m, listings.Select(item => item.TaxonomyId).Where(id => id > 0).Distinct().Count() * 2m);
        return (int)Math.Round(Math.Clamp(sales + reviews + favorites + views + seo + variety, 0m, 100m));
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
