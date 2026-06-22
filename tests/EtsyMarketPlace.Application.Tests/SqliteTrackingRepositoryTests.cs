namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using EtsyMarketPlace.Infrastructure.Tracking;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SqliteTrackingRepositoryTests
{
    [Fact]
    public async Task SaveCapture_SameExternalKeyCreatesOneItemWithMultipleSnapshots()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"etsy-tracking-{Guid.NewGuid():N}.db");
        try
        {
            var service = new TrackingService(new SqliteTrackingRepository(databasePath));
            await service.InitializeAsync();

            await service.TrackAsync(Capture(10, DateTimeOffset.UtcNow));
            await service.TrackAsync(Capture(25, DateTimeOffset.UtcNow.AddMinutes(1)));

            var history = Assert.Single(await service.GetHistoriesAsync());
            Assert.Equal(2, history.Snapshots.Count);
            Assert.Equal(25, history.Latest?.Favorites);
            Assert.Equal(10, history.Previous?.Favorites);

            await service.DeleteAsync(history.Item.Id);
            Assert.Empty(await service.GetHistoriesAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
        }
    }

    private static TrackingCapture Capture(int favorites, DateTimeOffset capturedAt) =>
        new(
            TrackingEntityType.Listing,
            "123456",
            "Example listing",
            "https://www.etsy.com/listing/123456",
            new TrackingSnapshot(0, 0, capturedAt, 35m, "USD", favorites, 1000, 500, SeoScore: 80, MarketScore: 70));

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
