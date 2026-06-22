namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Application.ShopPerformance;
using Xunit;

public sealed class AutomationRunServiceTests
{
    [Fact]
    public void IsDue_RequiresEnabledScheduleAndElapsedInterval()
    {
        var now = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        var settings = new AutomationSettings
        {
            Enabled = true,
            IntervalHours = 24,
            LastRunAt = now.AddHours(-25),
        };

        Assert.True(AutomationRunService.IsDue(settings, now));
        settings.LastRunAt = now.AddHours(-23);
        Assert.False(AutomationRunService.IsDue(settings, now));
        settings.Enabled = false;
        Assert.False(AutomationRunService.IsDue(settings, now.AddDays(2)));
    }

    [Fact]
    public void BuildAlerts_ReturnsRevenueAndOrderDropsAtConfiguredThreshold()
    {
        var settings = new AutomationSettings
        {
            RevenueDropAlertPercent = 20,
            OrderDropAlertPercent = 25,
        };
        var comparison = ShopPerformanceService.BuildComparison(
            Report(6, 12, 600m),
            Report(10, 16, 1000m));

        var alerts = AutomationRunService.BuildAlerts(comparison, settings);

        Assert.Contains(alerts, item => item.Title == "Ciro dususu");
        Assert.Contains(alerts, item => item.Title == "Siparis dususu");
    }

    private static ShopPerformanceReport Report(int orders, int units, decimal revenue) => new()
    {
        Shop = new OwnShopProfile(42, "Example", "https://www.etsy.com/shop/Example"),
        PeriodStart = new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero),
        PeriodEnd = new DateTimeOffset(2026, 6, 22, 23, 59, 59, TimeSpan.Zero),
        CurrencyCode = "USD",
        OrderCount = orders,
        UnitsSold = units,
        GrossRevenue = revenue,
        AverageOrderValue = orders == 0 ? 0 : revenue / orders,
        Products = [],
    };
}
