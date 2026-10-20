namespace EtsyMarketPlace.Domain.Entities;

using System;
using System.Collections.Generic;

public class TrackingItemEntity
{
    public long Id { get; set; }
    public int EntityType { get; set; }
    public string ExternalKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TrackingSnapshotEntity> Snapshots { get; set; } = new();
}

public class TrackingSnapshotEntity
{
    public long Id { get; set; }
    public long TrackingItemId { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public decimal? Price { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int? Favorites { get; set; }
    public int? Views { get; set; }
    public int? ShopSales { get; set; }
    public int? ReviewCount { get; set; }
    public decimal? ReviewAverage { get; set; }
    public int? SeoScore { get; set; }
    public int? MarketScore { get; set; }
    public int? DemandScore { get; set; }
    public int? CompetitionScore { get; set; }
    public int? OpportunityScore { get; set; }

    public TrackingItemEntity? TrackingItem { get; set; }
}
