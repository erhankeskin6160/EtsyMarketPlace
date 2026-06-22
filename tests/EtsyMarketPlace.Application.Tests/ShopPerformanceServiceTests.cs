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

    [Fact]
    public void BuildComparison_CalculatesKpisAndIncludesLostProducts()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var shop = new OwnShopProfile(42, "Example", "https://www.etsy.com/shop/Example");
        var current = ShopPerformanceService.Build(
            new OwnShopPerformanceSource(shop,
            [
                Receipt(1, start.AddDays(1), true, false, 30, [new(10, "Model A", 3, 10, "USD")]),
                Receipt(2, start.AddDays(2), true, false, 20, [new(20, "Model B", 1, 20, "USD")]),
            ]),
            start,
            start.AddDays(30));
        var previous = ShopPerformanceService.Build(
            new OwnShopPerformanceSource(shop,
            [
                Receipt(3, start.AddDays(-2), true, false, 20, [new(10, "Model A", 2, 10, "USD")]),
                Receipt(4, start.AddDays(-1), true, false, 15, [new(30, "Model C", 1, 15, "USD")]),
            ]),
            start.AddDays(-30),
            start.AddTicks(-1));

        var comparison = ShopPerformanceService.BuildComparison(current, previous);

        Assert.Equal(0, comparison.Orders.Difference);
        Assert.Equal(1, comparison.Units.Difference);
        Assert.Equal(15, comparison.Revenue.Difference);
        Assert.Equal(3, comparison.Products.Count);
        Assert.Contains(comparison.Products, item => item.ListingId == 30 && item.CurrentUnitsSold == 0 && item.PreviousUnitsSold == 1);
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
