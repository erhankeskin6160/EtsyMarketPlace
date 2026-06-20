namespace SimilarProductsWinForms.Services;

using SimilarProductsWinForms.Models;

internal static class ProfitCalculator
{
    private const decimal EtsyListingFeeUsd = 0.20m;
    private const decimal EtsyTransactionRate = 0.065m;

    public static ProfitCalculationResult Calculate(
        decimal salePrice,
        decimal materialCost,
        decimal laborHours,
        decimal hourlyRate,
        decimal packagingCost,
        decimal shippingCost,
        decimal adCost,
        decimal targetProfitMarginPercent)
    {
        var laborCost = laborHours * hourlyRate;
        var transactionFee = salePrice * EtsyTransactionRate;
        var totalCost = materialCost + laborCost + packagingCost + shippingCost + adCost + EtsyListingFeeUsd + transactionFee;
        var netProfit = salePrice - totalCost;
        var profitMargin = salePrice <= 0 ? 0 : netProfit / salePrice;

        var fixedCost = materialCost + laborCost + packagingCost + shippingCost + adCost + EtsyListingFeeUsd;
        var breakEvenPrice = fixedCost / (1 - EtsyTransactionRate);
        var targetRate = Math.Clamp(targetProfitMarginPercent / 100m, 0m, 0.90m);
        var targetPrice = fixedCost / (1 - EtsyTransactionRate - targetRate);

        return new ProfitCalculationResult(
            salePrice,
            materialCost,
            laborCost,
            packagingCost,
            shippingCost,
            adCost,
            EtsyListingFeeUsd,
            transactionFee,
            totalCost,
            netProfit,
            profitMargin,
            breakEvenPrice,
            targetPrice);
    }
}
