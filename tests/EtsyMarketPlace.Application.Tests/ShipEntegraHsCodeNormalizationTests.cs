namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Shipping;
using Xunit;

/// <summary>
/// ABD HTS normalizasyonu. Regresyon: 3926400000 kodu panelde 3926.40.00.00'e dönüşüyor,
/// bu kod gömülü listede yok; normalizasyon olmadan geçerli bir gönderi reddediliyordu.
/// </summary>
public sealed class ShipEntegraHsCodeNormalizationTests
{
    [Fact]
    public void IsUsCode_RejectsTenDigitCodeWithMissingStatisticalSuffix()
    {
        Assert.False(ShipEntegraHsCodeCatalog.IsUsCode("3926400000"));
    }

    [Fact]
    public void NormalizeForUs_FallsBackToEightDigitParent()
    {
        Assert.Equal("3926.40.00", ShipEntegraHsCodeCatalog.NormalizeForUs("3926400000"));
        Assert.Equal("3926.40.00", ShipEntegraHsCodeCatalog.NormalizeForUs("3926.40.00.00"));
    }

    [Fact]
    public void NormalizeForUs_LeavesAlreadyValidCodesUntouched()
    {
        Assert.Null(ShipEntegraHsCodeCatalog.NormalizeForUs("3926.40.00"));
        Assert.Null(ShipEntegraHsCodeCatalog.NormalizeForUs("3926.40.00.10"));
        Assert.Null(ShipEntegraHsCodeCatalog.NormalizeForUs("3926.10.00.00"));
    }

    [Fact]
    public void NormalizeForUs_DoesNotDropMeaningfulStatisticalDigits()
    {
        // Sıfır olmayan bir alt grup asla düşürülmez; kod reddedilmeye devam eder.
        Assert.Null(ShipEntegraHsCodeCatalog.NormalizeForUs("3926400099"));
    }

    [Fact]
    public void NormalizeForUs_ReturnsNullForUnknownOrEmptyCodes()
    {
        Assert.Null(ShipEntegraHsCodeCatalog.NormalizeForUs("9999999999"));
        Assert.Null(ShipEntegraHsCodeCatalog.NormalizeForUs(null));
        Assert.Null(ShipEntegraHsCodeCatalog.NormalizeForUs(""));
    }
}