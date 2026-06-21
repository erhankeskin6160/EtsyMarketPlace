namespace EtsyMarketPlace.Application.KeywordResearch;

using System.Text.RegularExpressions;
using EtsyMarketPlace.Domain.KeywordResearch;

public static class KeywordAnalysisCalculator
{
    private static readonly Regex WordRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "the", "for", "with", "from", "your", "you", "this", "that", "of", "to", "in", "on", "or", "a", "an",
        "ve", "ile", "icin", "bir", "bu", "da", "de", "veya",
    };

    public static KeywordAnalysisResult Calculate(KeywordMarketSample sample)
    {
        var listings = sample.Listings.ToList();
        var dominantCurrency = listings
            .Where(item => item.Price > 0 && !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .GroupBy(item => item.CurrencyCode)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? "";
        var prices = listings
            .Where(item => item.Price > 0 && item.CurrencyCode.Equals(dominantCurrency, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Price)
            .Order()
            .ToList();
        var favorites = listings.Select(item => (decimal)item.Favorites).Order().ToList();
        var views = listings.Select(item => (decimal)item.Views).Order().ToList();
        var averageSeo = listings.Count > 0 ? (int)Math.Round(listings.Average(item => item.SeoScore)) : 0;

        var competition = CalculateCompetition(sample.TotalResults, listings, prices, averageSeo);
        var demand = CalculateDemand(listings, Median(favorites), Median(views));
        var seoGap = 100 - averageSeo;
        var opportunity = (int)Math.Round(Math.Clamp(demand * 0.45m + (100 - competition) * 0.35m + seoGap * 0.20m, 0m, 100m));

        return new KeywordAnalysisResult
        {
            Keyword = sample.Keyword,
            Listings = listings,
            TotalResults = Math.Max(sample.TotalResults, listings.Count),
            SampleSize = listings.Count,
            PriceCurrency = dominantCurrency,
            MinimumPrice = prices.Count > 0 ? prices[0] : 0,
            MaximumPrice = prices.Count > 0 ? prices[^1] : 0,
            AveragePrice = prices.Count > 0 ? prices.Average() : 0,
            MedianPrice = Median(prices),
            MedianFavorites = Median(favorites),
            MedianViews = Median(views),
            AverageSeoScore = averageSeo,
            CompetitionScore = competition,
            DemandSignalScore = demand,
            OpportunityScore = opportunity,
            ConfidenceScore = CalculateConfidence(listings),
            TopTags = BuildFrequency(listings, item => item.Tags, 25),
            TopTitleTerms = BuildFrequency(listings, item => Tokenize(item.Title), 25),
            LongTailSuggestions = BuildFrequency(
                listings,
                item => item.Tags.Where(tag => tag.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2),
                25),
            AnalyzedAt = DateTimeOffset.Now,
        };
    }

    private static int CalculateCompetition(int totalResults, List<KeywordListingSnapshot> listings, List<decimal> prices, int averageSeo)
    {
        if (listings.Count == 0)
        {
            return 0;
        }

        var density = Math.Min(50m, (decimal)Math.Log10(Math.Max(1, totalResults) + 1) / 6m * 50m);
        var strongShopRatio = listings.Count(item => item.ShopSales >= 1000) * 20m / listings.Count;
        var seoDensity = Math.Min(15m, listings.Count(item => item.SeoScore >= 80) * 15m / listings.Count);
        var medianPrice = Median(prices);
        var priceCompression = medianPrice <= 0
            ? 0
            : Math.Clamp(15m - ((prices.Count > 0 ? prices[^1] - prices[0] : 0) / medianPrice * 5m), 0m, 15m);
        return (int)Math.Round(Math.Clamp(density + strongShopRatio + seoDensity + priceCompression, 0m, 100m));
    }

    private static int CalculateDemand(List<KeywordListingSnapshot> listings, decimal medianFavorites, decimal medianViews)
    {
        if (listings.Count == 0)
        {
            return 0;
        }

        var favoriteSignal = Math.Min(35m, (decimal)Math.Log10((double)medianFavorites + 1) * 14m);
        var viewSignal = Math.Min(35m, (decimal)Math.Log10((double)medianViews + 1) * 11m);
        var engagement = medianViews > 0 ? Math.Min(20m, medianFavorites / medianViews * 400m) : 0;
        var shopTraction = listings.Count(item => item.ShopSales >= 1000) * 10m / listings.Count;
        return (int)Math.Round(Math.Clamp(favoriteSignal + viewSignal + engagement + shopTraction, 0m, 100m));
    }

    private static int CalculateConfidence(List<KeywordListingSnapshot> listings)
    {
        if (listings.Count == 0)
        {
            return 0;
        }

        var sampleScore = Math.Min(60m, listings.Count * 0.6m);
        var completeFields = listings.Sum(item =>
            (item.Price > 0 ? 1 : 0) +
            (item.Views > 0 ? 1 : 0) +
            (item.Tags.Count > 0 ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(item.ShopName) ? 1 : 0));
        var completeness = completeFields * 40m / (listings.Count * 4m);
        return (int)Math.Round(Math.Clamp(sampleScore + completeness, 0m, 100m));
    }

    private static IReadOnlyList<KeywordFrequencyMetric> BuildFrequency(
        List<KeywordListingSnapshot> listings,
        Func<KeywordListingSnapshot, IEnumerable<string>> selector,
        int limit)
    {
        return listings
            .SelectMany(item => selector(item)
                .Select(value => value.Trim())
                .Where(value => value.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new KeywordFrequencyMetric(
                group.Key,
                group.Count(),
                listings.Count > 0 ? Math.Round(group.Count() * 100m / listings.Count, 1) : 0))
            .OrderByDescending(item => item.ListingCount)
            .ThenBy(item => item.Value, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static IEnumerable<string> Tokenize(string title) =>
        WordRegex.Matches(title.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length > 2 && !StopWords.Contains(word));

    private static decimal Median(IReadOnlyList<decimal> sorted)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2m : sorted[middle];
    }
}
