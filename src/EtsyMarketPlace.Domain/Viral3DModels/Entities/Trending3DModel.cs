namespace EtsyMarketPlace.Domain.Viral3DModels.Entities;

using System;
using System.Collections.Generic;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public class Trending3DModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ExternalId { get; set; } = string.Empty;
    public ModelPlatformType Platform { get; set; } = ModelPlatformType.MakerWorld;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorUrl { get; set; } = string.Empty;
    public string ModelPageUrl { get; set; } = string.Empty;
    public string PrimaryImageUrl { get; set; } = string.Empty;
    public List<string> GalleryImageUrls { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string Category { get; set; } = "Props & Decor";

    // Velocity & Popularity metrics
    public int Downloads24h { get; set; }
    public int TotalDownloads { get; set; }
    public int LikesCount { get; set; }
    public int PrintsCount { get; set; }

    // Delta Time-Series Velocity (Calculated via SQLite Historical Snapshots)
    public double HourlyVelocity { get; set; }
    public double GrowthRatePercentage { get; set; }
    public int HistoricalSnapshotsCount { get; set; }
    public bool IsDeltaAccelerating => HourlyVelocity >= 40.0 || GrowthRatePercentage >= 15.0;

    // License & Slicing specs
    public ModelLicenseInfo License { get; set; } = new();
    public PrintEstimation PrintSpecs { get; set; } = new();

    // Etsy Competition & Opportunity Score
    public int EtsyCompetitionCount { get; set; } = -1; // -1 = Henüz taranmadı
    public int OpportunityScore { get; set; } = 50;     // 0 - 100
    public DateTime DiscoveredAtUtc { get; set; } = DateTime.UtcNow;

    // Store Niche Fit & AI Matching
    public int ShopFitScore { get; set; } = 50;          // 0 - 100%
    public string ShopFitReason { get; set; } = string.Empty;
    public bool IsShopNicheMatch => ShopFitScore >= 70;

    public bool IsGoldenOpportunity => OpportunityScore >= 80 && (EtsyCompetitionCount is >= 0 and <= 5);

    public string SafeModelUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ModelPageUrl) && 
                ModelPageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !ModelPageUrl.Contains("5197816", StringComparison.OrdinalIgnoreCase) &&
                !ModelPageUrl.Contains("577943", StringComparison.OrdinalIgnoreCase))
            {
                return ModelPageUrl;
            }
            return GetPlatformSearchUrl();
        }
    }

    public string GetPlatformSearchUrl()
    {
        string clean = CleanTitleForSearch(Title);
        string encoded = Uri.EscapeDataString(clean);
        return Platform switch
        {
            ModelPlatformType.MakerWorld => $"https://makerworld.com/en/search/models?keyword={encoded}",
            ModelPlatformType.Printables => $"https://www.printables.com/search/models?q={encoded}",
            ModelPlatformType.Thingiverse => $"https://www.thingiverse.com/search?q={encoded}&page=1",
            ModelPlatformType.CrealityCloud => $"https://www.crealitycloud.com/search?keyword={encoded}",
            ModelPlatformType.MakerOnline => $"https://makeronline.com/search?keyword={encoded}",
            _ => $"https://www.google.com/search?q={encoded}+3d+model"
        };
    }

    private static string CleanTitleForSearch(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "3d print";
        int parenIdx = title.IndexOf('(');
        string s = parenIdx > 0 ? title.Substring(0, parenIdx) : title;
        int dashIdx = s.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0) s = s.Substring(0, dashIdx);
        return s.Trim();
    }
}
