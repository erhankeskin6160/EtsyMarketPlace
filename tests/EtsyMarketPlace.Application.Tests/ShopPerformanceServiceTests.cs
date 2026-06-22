using EtsyMarketPlace.Application.ShopPerformance;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public sealed class ShopPerformanceServiceTests
{
    [Fact]
    public void Build_UsesOnlyPaidNonCanceledReceipts_AndAggregatesProducts()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var source = new OwnShopPerformanceSource(
            new OwnShopProfile(42, "Example", "https://www.etsy.com/shop/Example"),
            [
                Receipt(1, start.AddDays(1), true, false, 30, [new(10, "Model A", 2, 10, "USD"), new(20, "Model B", 1, 10, "USD")]),
                Receipt(2, start.AddDays(2), true, false, 15, [new(10, "Model A", 1, 15, "USD")]),
                Receipt(3, start.AddDays(3), false, false, 99, [new(30, "Unpaid", 1, 99, "USD")]),
                Receipt(4, start.AddDays(4), true, true, 99, [new(40, "Canceled", 1, 99, "USD")]),
            ]);

        var report = ShopPerformanceService.Build(source, start, start.AddDays(30));

        Assert.Equal(2, report.OrderCount);
        Assert.Equal(4, report.UnitsSold);
        Assert.Equal(45, report.GrossRevenue);
        Assert.Equal(22.5m, report.AverageOrderValue);
        Assert.Equal(2, report.Products.Count);
        Assert.Equal(3, report.Products[0].UnitsSold);
        Assert.Equal(35, report.Products[0].Revenue);
    }

    private static OwnShopReceipt Receipt(
        long id,
        DateTimeOffset created,
        bool paid,
        bool canceled,
        decimal total,
        IReadOnlyList<OwnShopTransaction> transactions) =>
        new(id, created, paid, canceled, total, "USD", transactions);
}
