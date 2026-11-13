using System;
using EtsyMarketPlace.Domain.Accounting;
using EtsyMarketPlace.Domain.Accounting.Entities;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Accounting;

public sealed class DepreciationCalculatorTests
{
    [Fact]
    public void CalculateForPeriod_StraightLine_FullYearMatchesAnnualQuota()
    {
        var asset = new FixedAssetItem
        {
            Id = 1,
            ShopId = "53236321",
            AssetName = "Bambu Lab X1 Carbon",
            AssetAccountCode = "253",
            ExpenseTargetCategory = OperatingExpenseCategory.GeneralManagement_632,
            AcquisitionDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AcquisitionCostTry = 60_000m,
            SalvageValueTry = 0m,
            UsefulLifeYears = 5,
            Method = DepreciationMethod.StraightLine,
            IsActive = true
        };

        var line = DepreciationCalculator.CalculateForPeriod(
            asset,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero));

        Assert.Equal(12_000m, line.PeriodDepreciationTry);
        Assert.Equal(12_000m, line.AccumulatedDepreciationEndTry);
        Assert.Equal(48_000m, line.NetBookValueEndTry);
    }

    [Fact]
    public void CalculateForPeriod_DecliningBalance_CapsAt50PctAndZerosBookValueInFinalYear()
    {
        var asset = new FixedAssetItem
        {
            Id = 2,
            ShopId = "53236321",
            AssetName = "Reçine UV Kürleme İstasyonu",
            AssetAccountCode = "253",
            ExpenseTargetCategory = OperatingExpenseCategory.CostOfGoodsAdjustment_620,
            AcquisitionDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AcquisitionCostTry = 100_000m,
            SalvageValueTry = 0m,
            UsefulLifeYears = 3,
            Method = DepreciationMethod.DecliningBalance,
            IsActive = true
        };

        var y1 = DepreciationCalculator.CalculateForPeriod(
            asset,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero));
        var y2 = DepreciationCalculator.CalculateForPeriod(
            asset,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));
        var y3 = DepreciationCalculator.CalculateForPeriod(
            asset,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero));

        // 2/3 -> %50 tavan! Yıl 1: 50k, Yıl 2: 25k, Yıl 3 (kalan defter sıfırlama): 25k
        Assert.Equal(50_000m, y1.PeriodDepreciationTry);
        Assert.Equal(25_000m, y2.PeriodDepreciationTry);
        Assert.Equal(25_000m, y3.PeriodDepreciationTry);
        Assert.Equal(100_000m, y3.AccumulatedDepreciationEndTry);
        Assert.Equal(0m, y3.NetBookValueEndTry);
    }

    [Fact]
    public void CalculateForPeriod_InactiveOrBeforeAcquisition_ReturnsZeroDepreciation()
    {
        var asset = new FixedAssetItem
        {
            Id = 3,
            ShopId = "53236321",
            AssetName = "Ofis Bilgisayarı",
            AssetAccountCode = "255",
            AcquisitionDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            AcquisitionCostTry = 45_000m,
            UsefulLifeYears = 5,
            Method = DepreciationMethod.StraightLine,
            IsActive = true
        };

        var before = DepreciationCalculator.CalculateForPeriod(
            asset,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(0m, before.PeriodDepreciationTry);

        asset.IsActive = false;
        var inactive = DepreciationCalculator.CalculateForPeriod(
            asset,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(0m, inactive.PeriodDepreciationTry);
    }
}

