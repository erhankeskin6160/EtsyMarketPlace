namespace EtsyMarketPlace.Domain.Tracking;

public enum TrackingEntityType
{
    Listing = 1,
    Shop = 2,
    Keyword = 3,
}

public sealed record TrackingItem(
    long Id,
    TrackingEntityType EntityType,
    string ExternalKey,
    string DisplayName,
    string Url,
    DateTimeOffset CreatedAt);

public sealed record TrackingSnapshot(
    long Id,
    long TrackingItemId,
    DateTimeOffset CapturedAt,
    decimal? Price = null,
    string CurrencyCode = "",
    int? Favorites = null,
    int? Views = null,
    int? ShopSales = null,
    int? ReviewCount = null,
    decimal? ReviewAverage = null,
    int? SeoScore = null,
    int? MarketScore = null,
    int? DemandScore = null,
    int? CompetitionScore = null,
    int? OpportunityScore = null,
    int? ResultCount = null,
    int? SampleSize = null);

public sealed record TrackingCapture(
    TrackingEntityType EntityType,
    string ExternalKey,
    string DisplayName,
    string Url,
    TrackingSnapshot Snapshot);

public sealed record TrackingHistory(
    TrackingItem Item,
    IReadOnlyList<TrackingSnapshot> Snapshots)
{
    public TrackingSnapshot? Latest => Snapshots.OrderByDescending(item => item.CapturedAt).FirstOrDefault();
    public TrackingSnapshot? Previous => Snapshots.OrderByDescending(item => item.CapturedAt).Skip(1).FirstOrDefault();
}
