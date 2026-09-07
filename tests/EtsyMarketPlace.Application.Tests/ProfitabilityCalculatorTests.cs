namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Profitability;
using Xunit;

public sealed class ProfitabilityCalculatorTests
{
    private readonly ProfitabilityCalculator _calculator = new();

    [Fact]
    public void Calculate_TurkeySellerStandardItem_IncludesRegulatoryFee()
    {
        var input = new ProfitabilityInput(
            ItemPriceUsd: 50.0,
            BuyerShippingUsd: 10.0,
            GiftWrapUsd: 0.0,
            ProductBaseCostUsd: 12.0,
            SellerShippingCostUsd: 8.0,
            PackagingCostUsd: 2.0,
            OnPlatformAdsCostUsd: 1.0,
            SellerCountry: "Türkiye",
            OffsiteAds: OffsiteAdsMode.Disabled,
            ApplyCurrencyConversion: false,
            TargetProfitMarginPercent: 25.0);

        var result = _calculator.Calculate(input);

        Assert.Equal(60.0, result.TotalRevenueUsd); // $50 + $10 shipping
        Assert.True(result.Fees.ListingFeeUsd == 0.20);
        Assert.True(result.Fees.TransactionFeeUsd == 3.90); // 6.5% of $60 = $3.90
        Assert.True(result.Fees.RegulatoryOperatingFeeUsd > 0); // 1.67% of $60 = ~$1.00
        Assert.True(result.Fees.PaymentProcessingFeeUsd > 0); // 6.5% + $0.10
        Assert.True(result.NetProfitUsd > 0);
        Assert.True(result.ProfitMarginPercent > 0);
        Assert.True(result.BreakEvenItemPriceUsd > 0);
        Assert.True(result.RecommendedOptimalPriceUsd > 0);
    }

    [Fact]
    public void Calculate_UsSeller_ZeroRegulatoryFee()
    {
        var input = new ProfitabilityInput(
            ItemPriceUsd: 100.0,
            BuyerShippingUsd: 0.0,
            SellerCountry: "ABD");

        var result = _calculator.Calculate(input);

        Assert.Equal(0.0, result.Fees.RegulatoryOperatingFeeUsd);
        Assert.Equal(6.50, result.Fees.TransactionFeeUsd); // 6.5% of $100
        Assert.Equal(3.25, result.Fees.PaymentProcessingFeeUsd); // 3% + $0.25 = $3.25
    }

    [Fact]
    public void Calculate_OffsiteAdsCappedAt100Dollars()
    {
        var input = new ProfitabilityInput(
            ItemPriceUsd: 2000.0, // 15% of $2000 = $300, but cap is $100
            SellerCountry: "ABD",
            OffsiteAds: OffsiteAdsMode.Optional15Percent);

        var result = _calculator.Calculate(input);

        Assert.Equal(100.0, result.Fees.OffsiteAdsFeeUsd);
    }

    [Fact]
    public void Calculate_CurrencyConversion_Adds2Point5PercentFee()
    {
        var input = new ProfitabilityInput(
            ItemPriceUsd: 100.0,
            ApplyCurrencyConversion: true);

        var result = _calculator.Calculate(input);

        Assert.Equal(2.50, result.Fees.CurrencyConversionFeeUsd); // 2.5% of $100
    }

    [Fact]
    public void Calculate_BreakEvenPrice_YieldsZeroOrPositiveProfit()
    {
        var input = new ProfitabilityInput(
            ItemPriceUsd: 40.0,
            ProductBaseCostUsd: 15.0,
            SellerShippingCostUsd: 10.0,
            PackagingCostUsd: 2.0,
            SellerCountry: "Türkiye");

        var result = _calculator.Calculate(input);

        var breakEvenInput = input with { ItemPriceUsd = result.BreakEvenItemPriceUsd };
        var breakEvenResult = _calculator.Calculate(breakEvenInput);

        Assert.True(breakEvenResult.NetProfitUsd >= -0.05 && breakEvenResult.NetProfitUsd <= 0.20,
            $"Expected ~0 net profit at break even price, actual: {breakEvenResult.NetProfitUsd}");
    }

    [Fact]
    public void Calculate_RecommendedOptimalPrice_AchievesTargetMargin()
    {
        var input = new ProfitabilityInput(
            ItemPriceUsd: 30.0,
            ProductBaseCostUsd: 10.0,
            SellerShippingCostUsd: 5.0,
            TargetProfitMarginPercent: 30.0);

        var result = _calculator.Calculate(input);

        var optimalInput = input with { ItemPriceUsd = result.RecommendedOptimalPriceUsd };
        var optimalResult = _calculator.Calculate(optimalInput);

        Assert.True(optimalResult.ProfitMarginPercent >= 29.5 && optimalResult.ProfitMarginPercent <= 30.5,
            $"Expected ~30% margin at recommended price, actual: {optimalResult.ProfitMarginPercent}%");
    }
}
