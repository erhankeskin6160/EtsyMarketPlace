namespace EtsyMarketPlace.Application.Profitability;

/// <summary>
/// Offsite Ads tier selection.
/// </summary>
public enum OffsiteAdsMode
{
    Disabled = 0,
    Optional15Percent = 1,
    Mandatory12Percent = 2,
}

/// <summary>
/// Input parameter DTO for Etsy Profitability calculation.
/// </summary>
public sealed record ProfitabilityInput(
    double ItemPriceUsd,
    double BuyerShippingUsd = 0.0,
    double GiftWrapUsd = 0.0,
    double ProductBaseCostUsd = 0.0,
    double SellerShippingCostUsd = 0.0,
    double PackagingCostUsd = 0.0,
    double OnPlatformAdsCostUsd = 0.0,
    string SellerCountry = "Türkiye",
    OffsiteAdsMode OffsiteAds = OffsiteAdsMode.Disabled,
    bool ApplyCurrencyConversion = false,
    double TargetProfitMarginPercent = 25.0);

/// <summary>
/// Itemized fee breakdown for complete transparency.
/// </summary>
public sealed record ItemizedFeeBreakdown(
    double ListingFeeUsd,
    double TransactionFeeUsd,
    double PaymentProcessingFeeUsd,
    double RegulatoryOperatingFeeUsd,
    double OffsiteAdsFeeUsd,
    double CurrencyConversionFeeUsd,
    double TotalEtsyFeesUsd);

/// <summary>
/// Detailed profitability calculation result and AI-driven pricing recommendations.
/// </summary>
public sealed record ProfitabilityResult(
    double TotalRevenueUsd,
    double TotalExpensesUsd,
    ItemizedFeeBreakdown Fees,
    double NetProfitUsd,
    double ProfitMarginPercent,
    double ReturnOnInvestmentPercent,
    double BreakEvenItemPriceUsd,
    double RecommendedOptimalPriceUsd,
    double OffsiteAdsComparisonProfitUsd,
    string HealthRating,
    IReadOnlyList<string> Recommendations);
