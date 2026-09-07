namespace EtsyMarketPlace.Application.Dashboard;

using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;

public sealed class DashboardService(TrackingService trackingService)
{
    public async Task<DashboardOverview> GetOverviewAsync(CancellationToken cancellationToken = default) =>
        Build(await trackingService.GetHistoriesAsync(cancellationToken));

    public static DashboardOverview Build(IReadOnlyList<TrackingHistory> histories)
    {
        var opportunities = histories
            .Where(item => item.Item.EntityType == TrackingEntityType.Keyword && item.Latest?.OpportunityScore is not null)
            .Select(item => new DashboardOpportunity(
                item.Item.DisplayName,
                item.Latest!.OpportunityScore!.Value,
                item.Latest.DemandScore ?? 0,
                item.Latest.CompetitionScore ?? 0,
                item.Latest.CapturedAt))
            .OrderByDescending(item => item.OpportunityScore)
            .Take(10)
            .ToList();

        var changes = histories
            .Select(BuildChange)
            .Where(item => item is not null)
            .Cast<DashboardChange>()
            .OrderByDescending(item => Math.Abs(item.Difference))
            .Take(12)
            .ToList();

        return new DashboardOverview
        {
            TrackedItemCount = histories.Count,
            ListingCount = histories.Count(item => item.Item.EntityType == TrackingEntityType.Listing),
            ShopCount = histories.Count(item => item.Item.EntityType == TrackingEntityType.Shop),
            KeywordCount = histories.Count(item => item.Item.EntityType == TrackingEntityType.Keyword),
            SnapshotCount = histories.Sum(item => item.Snapshots.Count),
            TopOpportunities = opportunities,
            BiggestChanges = changes,
            Histories = histories,
        };
    }

    private static DashboardChange? BuildChange(TrackingHistory history)
    {
        var latest = history.Latest;
        var previous = history.Previous;
        if (latest is null || previous is null)
        {
            return null;
        }

        var metric = history.Item.EntityType switch
        {
            TrackingEntityType.Listing when latest.Favorites.HasValue && previous.Favorites.HasValue =>
                ("Favori", (decimal)previous.Favorites.Value, (decimal)latest.Favorites.Value),
            TrackingEntityType.Listing when latest.Views.HasValue && previous.Views.HasValue =>
                ("Goruntulenme", (decimal)previous.Views.Value, (decimal)latest.Views.Value),
            TrackingEntityType.Shop when latest.ShopSales.HasValue && previous.ShopSales.HasValue =>
                ("Magaza satisi", (decimal)previous.ShopSales.Value, (decimal)latest.ShopSales.Value),
            TrackingEntityType.Keyword when latest.OpportunityScore.HasValue && previous.OpportunityScore.HasValue =>
                ("Firsat puani", (decimal)previous.OpportunityScore.Value, (decimal)latest.OpportunityScore.Value),
            _ => default,
        };

        if (string.IsNullOrWhiteSpace(metric.Item1))
        {
            return null;
        }

        return new DashboardChange(
            history.Item.DisplayName,
            history.Item.EntityType,
            metric.Item1,
            metric.Item2,
            metric.Item3,
            metric.Item3 - metric.Item2,
            latest.CapturedAt);
    }
}
