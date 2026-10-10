namespace EtsyMarketPlace.Api.Models;

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
    public string Currency { get; set; } = "USD";
    public double AverageFavorites { get; set; }
    public double AverageViews { get; set; }
    public string TopShopName { get; set; } = "-";
    public int TopShopSales { get; set; }
    public int OpportunityScore { get; set; }
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
    public List<string> Tags { get; set; } = [];
    public List<string> Materials { get; set; } = [];
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public int ReviewCount { get; set; }
    public decimal ReviewAverage { get; set; }
    public int Quantity { get; set; }
}
