namespace EtsyMarketPlace.Application.Tests;

using System;
using Xunit;
using EtsyMarketPlace.Application.EtsyIntegration;

public sealed class HistoricalExchangeRateProviderTests
{
    [Fact]
    public void GetRateForDate_ReturnsExactKnownRate()
    {
        Assert.Equal(48.25m, HistoricalExchangeRateProvider.GetRateForDate(new DateTime(2026, 8, 15)));
    }

    [Fact]
    public void GetRateForDate_ResolvesExactOctoberRate_DistinctFromAugust()
    {
        var oct1Rate = HistoricalExchangeRateProvider.GetRateForDate(new DateTime(2026, 10, 1));
        var oct15Rate = HistoricalExchangeRateProvider.GetRateForDate(new DateTime(2026, 10, 15));
        var aug30Rate = HistoricalExchangeRateProvider.GetRateForDate(new DateTime(2026, 8, 30));

        Assert.Equal(49.03m, oct1Rate);
        Assert.Equal(49.42m, oct15Rate);
        Assert.Equal(48.26m, aug30Rate);
        Assert.NotEqual(oct1Rate, oct15Rate);
    }

    [Fact]
    public void GetRateForDate_FallsBack_WhenDateIsBeforeAllKnownRates()
    {
        Assert.Equal(48.25m, HistoricalExchangeRateProvider.GetRateForDate(new DateTime(2025, 5, 1), 48.25m));
    }
}
