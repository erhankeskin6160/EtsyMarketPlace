namespace EtsyMarketPlace.Application.Tests;

using System.Globalization;
using EtsyMarketPlace.Application.MarketResearch;
using Xunit;

public sealed class MarketCurrencyConverterTests
{
    [Theory]
    [InlineData("USD", "49.90", "49.90")]
    [InlineData("EUR", "100", "110.00")]
    [InlineData("GBP", "100", "130.00")]
    [InlineData("CAD", "100", "72.00")]
    [InlineData("IDR", "388303", "23.88")]
    [InlineData("TRY", "482.60", "10.00")]
    public void TryConvertToUsd_KnownCurrencies_ConvertsToUsd(string currency, string amount, string expected)
    {
        var result = MarketCurrencyConverter.TryConvertToUsd(
            decimal.Parse(amount, CultureInfo.InvariantCulture),
            currency);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void TryConvertToUsd_InvalidInputs_ReturnNull()
    {
        Assert.Null(MarketCurrencyConverter.TryConvertToUsd(100m, "XYZ"));
        Assert.Null(MarketCurrencyConverter.TryConvertToUsd(100m, ""));
        Assert.Null(MarketCurrencyConverter.TryConvertToUsd(100m, null));
        Assert.Null(MarketCurrencyConverter.TryConvertToUsd(0m, "USD"));
        Assert.Null(MarketCurrencyConverter.TryConvertToUsd(-5m, "USD"));
    }

    [Fact]
    public void IsSupported_MatchesRateTable()
    {
        Assert.True(MarketCurrencyConverter.IsSupported("usd"));
        Assert.True(MarketCurrencyConverter.IsSupported("EUR"));
        Assert.False(MarketCurrencyConverter.IsSupported("XYZ"));
        Assert.False(MarketCurrencyConverter.IsSupported(""));
    }
}
