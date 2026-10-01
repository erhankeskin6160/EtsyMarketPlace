namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class EtsyAdvancedSyncIntegrationTests : IAsyncLifetime
{
    private readonly string _tempDbPath;
    private readonly SqliteEtsyIntegrationStore _store;

    public EtsyAdvancedSyncIntegrationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"etsy-test-{Guid.NewGuid():N}.db");
        var options = Options.Create(new EtsyIntegrationOptions { DatabasePath = _tempDbPath });
        var dummyProtector = new DummyDataProtector();
        _store = new SqliteEtsyIntegrationStore(options, dummyProtector);
    }

    public Task InitializeAsync() => _store.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task MonthlyOrderSummaries_SaveAndRetrieve_Succeeds()
    {
        const string shopId = "test_shop_123";
        var summaries = new List<EtsyMonthlyOrderSummary>
        {
            new(shopId, "2026-09", 42, 55, 1250.50m, 29.77m, 40, 2, "USD"),
            new(shopId, "2026-08", 38, 48, 980.00m, 25.79m, 38, 0, "USD")
        };

        await _store.SaveMonthlyOrderSummariesAsync(summaries);
        var retrieved = await _store.GetMonthlyOrderSummariesAsync(shopId, months: 12);

        Assert.Equal(2, retrieved.Count);
        Assert.Equal("2026-09", retrieved[0].YearMonth);
        Assert.Equal(42, retrieved[0].OrderCount);
        Assert.Equal(1250.50m, retrieved[0].GrossRevenue);
        Assert.Equal(2, retrieved[0].UnfulfilledOrders);
    }

    [Fact]
    public async Task ListingTrafficDaily_SaveAndRetrieve_Succeeds()
    {
        const string shopId = "test_shop_123";
        var records = new List<EtsyListingTrafficRecord>
        {
            new(shopId, 998877, "2026-10-01", "Handmade Wooden Lamp", 150, 45, 12, 3, 8, 240.00m, 5.33m, "http://img.jpg", "http://etsy.com/1"),
            new(shopId, 112233, "2026-10-01", "Ceramic Mug", 80, 20, 5, 1, 2, 40.00m, 2.50m, "http://img2.jpg", "http://etsy.com/2")
        };

        await _store.SaveListingTrafficDailyAsync(records);
        var retrieved = await _store.GetListingTrafficAnalyticsAsync(shopId, "2026-10-01");

        Assert.Equal(2, retrieved.Count);
        Assert.Equal(998877, retrieved[0].ListingId);
        Assert.Equal(150, retrieved[0].Views);
        Assert.Equal(5.33m, retrieved[0].ConversionRate);
    }

    [Fact]
    public async Task ChartSnapshots_SaveAndRetrieve_Succeeds()
    {
        const string shopId = "test_shop_123";
        const string base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
        var start = DateTimeOffset.UtcNow.AddDays(-30);
        var end = DateTimeOffset.UtcNow;

        var snapshot = new EtsyChartSnapshot(shopId, "cost_pie", start, end, base64Png, DateTimeOffset.UtcNow);
        await _store.SaveChartSnapshotAsync(snapshot);

        var retrieved = await _store.GetChartSnapshotAsync(shopId, "cost_pie");

        Assert.NotNull(retrieved);
        Assert.Equal("cost_pie", retrieved!.ChartType);
        Assert.Equal(base64Png, retrieved.ImagePngBase64);
    }

    private sealed class DummyDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => plaintext;
        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
