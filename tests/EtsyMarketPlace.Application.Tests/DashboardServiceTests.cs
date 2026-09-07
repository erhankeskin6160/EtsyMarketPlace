namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Dashboard;
using EtsyMarketPlace.Domain.Tracking;
using Xunit;

public sealed class DashboardServiceTests
{
    [Fact]
    public void Build_CalculatesKpisOpportunitiesAndChanges()
    {
        var now = DateTimeOffset.UtcNow;
        var listing = new TrackingHistory(
            new TrackingItem(1, TrackingEntityType.Listing, "1", "Listing", "", now),
            [
                new TrackingSnapshot(2, 1, now.AddMinutes(1), Favorites: 25),
                new TrackingSnapshot(1, 1, now, Favorites: 10),
            ]);
        var keyword = new TrackingHistory(
            new TrackingItem(2, TrackingEntityType.Keyword, "sword", "sword", "", now),
            [
                new TrackingSnapshot(4, 2, now.AddMinutes(1), DemandScore: 75, CompetitionScore: 40, OpportunityScore: 70),
                new TrackingSnapshot(3, 2, now, DemandScore: 60, CompetitionScore: 45, OpportunityScore: 50),
            ]);

        var result = DashboardService.Build([listing, keyword]);

        Assert.Equal(2, result.TrackedItemCount);
        Assert.Equal(1, result.ListingCount);
        Assert.Equal(1, result.KeywordCount);
        Assert.Equal(4, result.SnapshotCount);
        Assert.Equal(70, Assert.Single(result.TopOpportunities).OpportunityScore);
        Assert.Equal("Firsat puani", result.BiggestChanges[0].MetricName);
        Assert.Equal(20, result.BiggestChanges[0].Difference);
    }
}
