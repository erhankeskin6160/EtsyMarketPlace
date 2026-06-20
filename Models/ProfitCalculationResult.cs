namespace SimilarProductsWinForms.Models;

internal sealed record ProfitCalculationResult(
    decimal SalePrice,
    decimal MaterialCost,
    decimal LaborCost,
    decimal PackagingCost,
    decimal ShippingCost,
    decimal AdCost,
    decimal ListingFee,
    decimal TransactionFee,
    decimal TotalCost,
    decimal NetProfit,
    decimal ProfitMargin,
    decimal BreakEvenPrice,
    decimal TargetPrice);
