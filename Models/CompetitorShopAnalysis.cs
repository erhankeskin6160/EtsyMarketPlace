namespace SimilarProductsWinForms.Models;

internal sealed class CompetitorShopProfile
{
    public long ShopId { get; init; }
    public string ShopName { get; init; } = "";
    public string ShopUrl { get; init; } = "";
    public string Title { get; init; } = "";
    public int TotalSales { get; init; }
    public int ReviewCount { get; init; }
    public decimal ReviewAverage { get; init; }
    public int ActiveListingCount { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public decimal DailySalesEstimate { get; init; }
    public decimal MonthlyRevenueEstimate { get; init; }
}

internal sealed class CompetitorShopAnalysis
{
    public required CompetitorShopProfile Shop { get; init; }
    public required List<MarketListingResult> Listings { get; init; }
    public required List<FrequencyMetric> TopTags { get; init; }
    public required List<FrequencyMetric> TopTitleTerms { get; init; }
    public required List<FrequencyMetric> TaxonomyDistribution { get; init; }
    public decimal MinimumPrice { get; init; }
    public decimal MaximumPrice { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal MedianPrice { get; init; }
    public decimal AverageFavorites { get; init; }
    public decimal AverageViews { get; init; }
    public int AverageSeoScore { get; init; }
    public int CompetitorStrengthScore { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }
    public string CurrencyDisplay { get; init; } = "";
    public string PriceCurrency { get; init; } = "";
    public decimal EstimatedDailySalesVelocity { get; init; }
    public decimal EstimatedMonthlyTurnover { get; init; }
    public string AiCompetitiveGapInsight { get; set; } = "";
}

internal sealed record FrequencyMetric(string Name, int Count, decimal Percentage)
{
    public string PercentageDisplay => $"%{Percentage:0.#}";
}
