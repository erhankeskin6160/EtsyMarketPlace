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
    public void GetRateForDate_UsesNearestPastRate_ForOctoberEntries()
    {
        Assert.Equal(48.26m, HistoricalExchangeRateProvider.GetRateForDate(new DateTime(2026, 10, 1)));
    }

    [Fact]
    public void GetRateForDate_FallsBack_WhenDateIsBeforeAllKnownRates()
    {
        Assert.Equal(48.25m, HistoricalExchangeRateProvider.GetRateForDate(new DateTime(2025, 5, 1), 48.25m));
    }
}
