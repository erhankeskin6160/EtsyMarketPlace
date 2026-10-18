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

public sealed class CompetitorShopProfileDto
{
    public long ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ShopUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int TotalSales { get; set; }
    public int TotalSalesCount => TotalSales;
    public int ReviewCount { get; set; }
    public decimal ReviewAverage { get; set; }
    public int ActiveListingCount { get; set; }
    public int ActiveListingsCount => ActiveListingCount;
    public DateTimeOffset? CreatedDate { get; set; }
    public int ShopAgeDays => CreatedDate.HasValue
        ? Math.Max(1, (int)Math.Round((DateTimeOffset.UtcNow - CreatedDate.Value).TotalDays))
        : Math.Clamp(TotalSales * 2, 30, 1800);
    public decimal DailySalesEstimate { get; set; }
    public decimal DailySalesVelocity => DailySalesEstimate;
    public int EstimatedMonthlySales => (int)Math.Round(DailySalesEstimate * 30m);
    public decimal MonthlyRevenueEstimate { get; set; }
    public decimal EstimatedMonthlyRevenueUsd => MonthlyRevenueEstimate;
    public decimal AveragePriceUsd { get; set; }
    public decimal MinPriceUsd { get; set; }
    public decimal MaxPriceUsd { get; set; }
    public decimal MedianPriceUsd { get; set; }
    public int CompetitorStrengthScore { get; set; }
    public string AverageLqsGrade { get; set; } = "B";
    public int BestsellerCount { get; set; }
}

public sealed class CompetitorListingDto
{
    public long Id { get; set; }
    public long ListingId => Id;
    public int Rank { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal PriceUsd { get; set; }
    public string Currency { get; set; } = "USD";
    public string ListingUrl { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public int Favorites { get; set; }
    public int Views { get; set; }
    public int Quantity { get; set; }
    public int SeoScore { get; set; }
    public string LqsGrade { get; set; } = "B";
    public int LqsScore { get; set; }
    public string LqsExplanation { get; set; } = string.Empty;
    public int EstimatedMonthlySales { get; set; }
    public decimal EstimatedMonthlyRevenue { get; set; }
    public decimal EstimatedMonthlyRevenueUsd => EstimatedMonthlyRevenue;
    public string SalesBadge { get; set; } = string.Empty;
    public string SalesVelocityBadge => string.IsNullOrWhiteSpace(SalesBadge) ? "📈 İstikrarlı" : SalesBadge;
    public int TitleCharLength { get; set; }
    public int MissingTagCount { get; set; }
    public int MultiWordTagCount { get; set; }
    public int TagCount => Tags?.Count ?? 0;
    public int ImageCount => ImageUrls?.Count > 0 ? ImageUrls.Count : (string.IsNullOrWhiteSpace(ImageUrl) ? 0 : 1);
    public int DescriptionLength => Description?.Length ?? 0;
    public List<string> Tags { get; set; } = [];
    public List<string> Materials { get; set; } = [];
    public string Description { get; set; } = string.Empty;
}

public sealed class CompetitorFrequencyItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Term => Name;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
    public decimal UsagePercentage => Percentage;
    public bool IsLongTail => Name.Contains(' ');
}

public sealed class CompetitorShopAnalysisDto
{
    public bool Found => Shop.ShopId > 0 || Listings.Count > 0 || !string.IsNullOrWhiteSpace(Shop.ShopName);
    public string? ErrorMessage { get; set; }
    public CompetitorShopProfileDto Shop { get; set; } = new();
    public List<CompetitorListingDto> Listings { get; set; } = [];
    public List<CompetitorFrequencyItemDto> TopTags { get; set; } = [];
    public List<CompetitorFrequencyItemDto> TopTitleTerms { get; set; } = [];
    public List<CompetitorFrequencyItemDto> TopTitleWords => TopTitleTerms;
    public decimal MinimumPrice { get; set; }
    public decimal MaximumPrice { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal MedianPrice { get; set; }
    public string PriceCurrency { get; set; } = "USD";
    public decimal AverageFavorites { get; set; }
    public decimal AverageViews { get; set; }
    public int AverageSeoScore { get; set; }
    public int CompetitorStrengthScore { get; set; }
    public decimal EstimatedDailySalesVelocity { get; set; }
    public decimal EstimatedMonthlyTurnover { get; set; }
    public string AiCompetitiveGapInsight { get; set; } = string.Empty;
    public DateTimeOffset RetrievedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CompetitorVariationGroupDto
{
    public string Name { get; set; } = string.Empty;
    public List<string> Values { get; set; } = [];
}

public sealed class CompetitorVariationOfferingDto
{
    public string Key { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class CompetitorListingInventoryDto
{
    public long ListingId { get; set; }
    public bool HasVariations { get; set; }
    public string? VarType1 { get; set; }
    public string? VarValues1 { get; set; }
    public bool EnableVar2 { get; set; }
    public string? VarType2 { get; set; }
    public string? VarValues2 { get; set; }
    public List<CompetitorVariationGroupDto> Groups { get; set; } = [];
    public List<CompetitorVariationOfferingDto> Offerings { get; set; } = [];
}

public sealed record AiLqsImprovementCommand(
    long ListingId,
    string Title,
    List<string>? Tags,
    string? Description,
    List<string>? Materials,
    decimal PriceUsd,
    string? Category,
    int CurrentLqsScore,
    string? Provider,
    string? ApiKey,
    string? Model
);

public sealed class AiLqsImprovementResultDto
{
    public long ListingId { get; set; }
    public string OptimizedTitle { get; set; } = string.Empty;
    public List<string> OptimizedTags { get; set; } = [];
    public string OptimizedDescription { get; set; } = string.Empty;
    public List<string> SuggestedMaterials { get; set; } = [];
    public List<CompetitorVariationGroupDto> SuggestedVariations { get; set; } = [];
    public List<CompetitorVariationOfferingDto> SuggestedOfferings { get; set; } = [];
    public int TargetLqsScore { get; set; } = 100;
    public string TargetLqsGrade { get; set; } = "A+";
    public string LqsDoctorAuditSummary { get; set; } = string.Empty;
    public List<string> ImprovementHighlights { get; set; } = [];
    public bool UsedAi { get; set; }
    public string Provider { get; set; } = "SmartMarketEngine";
}

