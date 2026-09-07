namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Infrastructure.ListingOptimization;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SqliteListingOptimizationHistoryRepositoryTests
{
    [Fact]
    public async Task SaveAndReadRecent_PersistsOptimizationVersion()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"etsy-optimization-{Guid.NewGuid():N}.db");
        try
        {
            var service = new ListingOptimizationHistoryService(
                new SqliteListingOptimizationHistoryRepository(databasePath));
            await service.InitializeAsync();

            var saved = await service.SaveAsync(new SaveListingOptimizationHistory(
                "123",
                "Dragon wall decor",
                "dragon wall decor",
                new ListingOptimizationResult(
                    58,
                    88,
                    ["Dragon wall decor gift"],
                    ["dragon decor", "wall gift"],
                    ["resin"],
                    "Draft",
                    ["gift"],
                    ["Risk"],
                    ["Checklist"])));

            var recent = await service.GetRecentAsync();
            var entry = Assert.Single(recent);
            Assert.Equal(saved.Id, entry.Id);
            Assert.Equal("Dragon wall decor", entry.ListingTitle);
            Assert.Equal(58, entry.CurrentSeoScore);
            Assert.Equal(88, entry.OptimizedSeoScore);
            Assert.Equal(["dragon decor", "wall gift"], entry.SuggestedTags);

            await service.DeleteAsync(entry.Id);
            Assert.Empty(await service.GetRecentAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
