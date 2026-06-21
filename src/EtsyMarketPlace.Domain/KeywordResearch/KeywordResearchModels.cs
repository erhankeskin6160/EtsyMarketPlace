namespace EtsyMarketPlace.Domain.KeywordResearch;

public sealed record KeywordListingSnapshot(
    long ListingId,
    string Title,
    string Description,
    decimal Price,
    string CurrencyCode,
    int Favorites,
    int Views,
    int Quantity,
    int ShopSales,
    int SeoScore,
    int MarketScore,
    string ShopName,
    string ShopUrl,
    string ListingUrl,
    string ImageUrl,
    IReadOnlyList<string> Tags);

public sealed record KeywordMarketSample(
    string Keyword,
    int TotalResults,
    IReadOnlyList<KeywordListingSnapshot> Listings);

public sealed record KeywordFrequencyMetric(string Value, int ListingCount, decimal Percentage)
{
    public string PercentageDisplay => $"%{Percentage:0.#}";
}

public sealed class KeywordAnalysisResult
{
    public required string Keyword { get; init; }
    public required IReadOnlyList<KeywordListingSnapshot> Listings { get; init; }
    public required IReadOnlyList<KeywordFrequencyMetric> TopTags { get; init; }
    public required IReadOnlyList<KeywordFrequencyMetric> TopTitleTerms { get; init; }
    public required IReadOnlyList<KeywordFrequencyMetric> LongTailSuggestions { get; init; }
    public int TotalResults { get; init; }
    public int SampleSize { get; init; }
    public string PriceCurrency { get; init; } = "";
    public decimal MinimumPrice { get; init; }
    public decimal MaximumPrice { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal MedianPrice { get; init; }
    public decimal MedianFavorites { get; init; }
    public decimal MedianViews { get; init; }
    public int AverageSeoScore { get; init; }
    public int CompetitionScore { get; init; }
    public int DemandSignalScore { get; init; }
    public int OpportunityScore { get; init; }
    public int ConfidenceScore { get; init; }
    public DateTimeOffset AnalyzedAt { get; init; }
}
