namespace EtsyMarketPlace.Application.Profitability;

/// <summary>
/// Contains official 2026 Etsy fee percentages, country-specific payment processing rates,
/// and regulatory operating fee rates based on Etsy Seller Handbook documentation.
/// </summary>
public static class EtsyFeeRules
{
    /// <summary>
    /// Standard listing fee charged per item listed or renewed ($0.20 USD).
    /// </summary>
    public const double ListingFeeUsd = 0.20;

    /// <summary>
    /// Mandatory Etsy transaction fee percentage applied to total order amount (6.5%).
    /// </summary>
    public const double TransactionFeePercent = 6.5;

    /// <summary>
    /// Maximum cap for Offsite Ads fee per single order ($100 USD).
    /// </summary>
    public const double OffsiteAdsCapUsd = 100.00;

    /// <summary>
    /// Currency conversion fee percentage if listing currency differs from payout currency (2.5%).
    /// </summary>
    public const double CurrencyConversionPercent = 2.5;

    /// <summary>
    /// Offsite Ads fee percentage for high-revenue sellers (annual sales > $10k USD).
    /// </summary>
    public const double OffsiteAdsMandatoryPercent = 12.0;

    /// <summary>
    /// Offsite Ads fee percentage for optional enrollment (annual sales < $10k USD).
    /// </summary>
    public const double OffsiteAdsOptionalPercent = 15.0;

    /// <summary>
    /// Returns the Regulatory Operating Fee percentage for the seller's registered country.
    /// Official Etsy rates (updated 2026):
    /// - Türkiye: 1.67%
    /// - United Kingdom: 0.48%
    /// - France: 1.14%
    /// - Spain: 0.88%
    /// - Italy: 0.80%
    /// - Hungary: 1.97%
    /// - US / Other: 0.00%
    /// </summary>
    public static double GetRegulatoryFeePercent(string countryCodeOrName)
    {
        if (string.IsNullOrWhiteSpace(countryCodeOrName)) return 0.0;
        var normalized = countryCodeOrName.Trim().ToLowerInvariant();

        if (normalized.Contains("tr") || normalized.Contains("turk") || normalized.Contains("türki"))
            return 1.67;
        if (normalized.Contains("uk") || normalized.Contains("united kingdom") || normalized.Contains("ingil"))
            return 0.48;
        if (normalized.Contains("fr") || normalized.Contains("france") || normalized.Contains("fran"))
            return 1.14;
        if (normalized.Contains("es") || normalized.Contains("spain") || normalized.Contains("ispan"))
            return 0.88;
        if (normalized.Contains("it") || normalized.Contains("italy") || normalized.Contains("ital"))
            return 0.80;
        if (normalized.Contains("hu") || normalized.Contains("hungary") || normalized.Contains("macar"))
            return 1.97;

        return 0.0;
    }

    /// <summary>
    /// Returns the Payment Processing Fee rates (% and fixed fee USD equivalent) for the seller's region.
    /// - Türkiye: 6.5% + $0.10 (approx ~3.00 TRY equivalent)
    /// - US: 3.0% + $0.25
    /// - UK: 4.0% + $0.25 (approx £0.20 equivalent)
    /// - EU: 4.0% + $0.32 (approx €0.30 equivalent)
    /// </summary>
    public static (double Percent, double FixedUsd) GetPaymentProcessingRate(string countryCodeOrName)
    {
        if (string.IsNullOrWhiteSpace(countryCodeOrName)) return (3.0, 0.25);
        var normalized = countryCodeOrName.Trim().ToLowerInvariant();

        if (normalized.Contains("tr") || normalized.Contains("turk") || normalized.Contains("türki"))
            return (6.5, 0.10);
        if (normalized.Contains("uk") || normalized.Contains("united kingdom") || normalized.Contains("ingil"))
            return (4.0, 0.25);
        if (normalized.Contains("fr") || normalized.Contains("es") || normalized.Contains("it") || normalized.Contains("eu") || normalized.Contains("avrupa"))
            return (4.0, 0.32);

        return (3.0, 0.25);
    }
}
