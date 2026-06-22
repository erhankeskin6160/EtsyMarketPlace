namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Infrastructure.ShopPerformance;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SqliteShopPerformanceHistoryRepositoryTests
{
    [Fact]
    public async Task SaveAsync_SameShopPeriodAndDayUpdatesSnapshotAndProducts()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"etsy-shop-history-{Guid.NewGuid():N}.db");
        try
        {
            var service = new ShopPerformanceHistoryService(
                new SqliteShopPerformanceHistoryRepository(databasePath));
            await service.InitializeAsync();
            var captured = new DateTimeOffset(2026, 6, 22, 10, 0, 0, TimeSpan.FromHours(3));

            await service.SaveAsync(Report(2, 3, 45m, [Product(10, 3, 30m)]), captured);
            await service.SaveAsync(Report(4, 6, 90m, [Product(10, 5, 75m), Product(20, 1, 15m)]), captured.AddHours(2));

            var history = Assert.Single(await service.GetHistoryAsync(42));
            Assert.Equal(4, history.OrderCount);
            Assert.Equal(6, history.UnitsSold);
            Assert.Equal(90m, history.GrossRevenue);
            Assert.Equal(2, history.Products.Count);
            Assert.Equal(5, history.Products[0].UnitsSold);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
        }
    }

    private static ShopPerformanceReport Report(
        int orders,
        int units,
        decimal revenue,
        IReadOnlyList<ProductPerformance> products) =>
        new()
        {
            Shop = new OwnShopProfile(42, "Example", "https://www.etsy.com/shop/Example"),
            PeriodStart = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            PeriodEnd = new DateTimeOffset(2026, 6, 21, 23, 59, 59, TimeSpan.Zero),
            CurrencyCode = "USD",
            OrderCount = orders,
            UnitsSold = units,
            GrossRevenue = revenue,
            AverageOrderValue = orders == 0 ? 0 : revenue / orders,
            Products = products,
        };

    private static ProductPerformance Product(long listingId, int units, decimal revenue) =>
        new(listingId, $"Listing {listingId}", 1, units, revenue, "USD");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
