namespace EtsyMarketPlace.Application.MarketResearch;

using System.Collections.Generic;

public sealed record MarketSearchQuery(
    string Keyword,
    int Limit = 50,
    string SortBy = "market_score",
    string ShopId = "53236321"
);

public sealed class MarketSearchResponse
{
    public string Keyword { get; set; } = string.Empty;
    public int Total { get; set; }
    public MarketSummaryKpisDto Kpis { get; set; } = new();
    public List<MarketListingItemDto> Listings { get; set; } = [];
}

public sealed class MarketSummaryKpisDto
{
    public int TotalListings { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal MedianPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public double AverageFavorites { get; set; }
    public double AverageViews { get; set; }
    public string TopShopName { get; set; } = "-";
    public int TopShopSales { get; set; }
    public int UniqueShopCount { get; set; }
    public double TopShopConcentrationPct { get; set; }
    public double FreeShippingRatioPct { get; set; }
    public int BestsellerCount { get; set; }
    public int HighOpportunityCount { get; set; }
    public int OpportunityScore { get; set; }
    public decimal TotalEstimatedMarketRevenue { get; set; }
    public double AverageEstimatedMonthlySales { get; set; }
    public decimal TopSellerMonthlyRevenue { get; set; }
    public List<MarketTagFrequencyDto> TopTags { get; set; } = [];
}

public sealed class MarketTagFrequencyDto
{
    public string Tag { get; set; } = string.Empty;
    public int Count { get; set; }
    public double UsagePercentage { get; set; }
    public int WordCount { get; set; }
    public int CharLength { get; set; }
    public bool IsLongTail => WordCount >= 2;
    public string CompetitionLevel { get; set; } = "Orta";
}

public sealed class MarketListingItemDto
{
    public long Id { get; set; }
    public int ListingRank { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal PriceUsd { get; set; }
    public string Currency { get; set; } = "USD";
    public string ShopName { get; set; } = string.Empty;
    public int ShopSales { get; set; }
    public string ShopUrl { get; set; } = string.Empty;
    public string ListingUrl { get; set; } = string.Empty;
    public int Favorites { get; set; }
    public int Views { get; set; }
    public int SeoScore { get; set; }
    public int MarketScore { get; set; }
    public int EstimatedMonthlySales { get; set; }
    public decimal EstimatedMonthlyRevenue { get; set; }
    public string SalesVelocity { get; set; } = "Düzenli";
    public decimal ConversionRateEst { get; set; }
    public bool IsBestseller { get; set; }
    public bool HasFreeShipping { get; set; }
    public bool IsOpportunityListing { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Materials { get; set; } = [];
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public int ReviewCount { get; set; }
    public decimal ReviewAverage { get; set; }
    public int Quantity { get; set; }
}

public sealed record MarketAiReportCommand(
    string Keyword,
    string ShopId = "53236321",
    decimal AveragePrice = 0,
    decimal MinPrice = 0,
    decimal MaxPrice = 0,
    decimal MedianPrice = 0,
    double AverageFavorites = 0,
    double AverageViews = 0,
    int OpportunityScore = 0,
    decimal TotalEstimatedMarketRevenue = 0,
    int UniqueShopCount = 0,
    int TotalListings = 0,
    string TopShopName = "-",
    int TopShopSales = 0,
    IReadOnlyList<string>? TopCompetitorTitles = null,
    IReadOnlyList<string>? TopTags = null,
    string? ApiKey = null,
    string? Model = null,
    string? Provider = null
);

public sealed class MarketAiReportResultDto
{
    public string Keyword { get; set; } = string.Empty;
    public bool UsedAi { get; set; }
    public string Provider { get; set; } = "Deterministic";
    public string Model { get; set; } = "RuleEngine";
    public string MarketOverview { get; set; } = string.Empty;
    public string PricingStrategy { get; set; } = string.Empty;
    public List<string> DifferentiationIdeas { get; set; } = [];
    public List<string> RecommendedKeywords { get; set; } = [];
    public List<string> ActionItems { get; set; } = [];
    public string ReportHtml { get; set; } = string.Empty;
}
