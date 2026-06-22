namespace EtsyMarketPlace.Application.Dashboard;

using EtsyMarketPlace.Domain.Tracking;

public sealed record DashboardOpportunity(
    string Keyword,
    int OpportunityScore,
    int DemandScore,
    int CompetitionScore,
    DateTimeOffset CapturedAt);

public sealed record DashboardChange(
    string DisplayName,
    TrackingEntityType EntityType,
    string MetricName,
    decimal PreviousValue,
    decimal LatestValue,
    decimal Difference,
    DateTimeOffset CapturedAt);

public sealed class DashboardOverview
{
    public int TrackedItemCount { get; init; }
    public int ListingCount { get; init; }
    public int ShopCount { get; init; }
    public int KeywordCount { get; init; }
    public int SnapshotCount { get; init; }
    public required IReadOnlyList<DashboardOpportunity> TopOpportunities { get; init; }
    public required IReadOnlyList<DashboardChange> BiggestChanges { get; init; }
    public required IReadOnlyList<TrackingHistory> Histories { get; init; }
}
