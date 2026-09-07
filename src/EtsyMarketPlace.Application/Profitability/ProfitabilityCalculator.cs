namespace EtsyMarketPlace.Application.Profitability;

/// <summary>
/// Calculation engine for Etsy seller fees, net profit, margin %, break-even price,
/// target margin pricing, and Offsite Ads comparison.
/// </summary>
public sealed class ProfitabilityCalculator
{
    /// <summary>
    /// Calculates complete profitability report from the given inputs.
    /// </summary>
    public ProfitabilityResult Calculate(ProfitabilityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var totalRevenue = Math.Max(0.0, input.ItemPriceUsd + input.BuyerShippingUsd + input.GiftWrapUsd);
        var fees = CalculateFees(totalRevenue, input);

        var directCosts = input.ProductBaseCostUsd + input.SellerShippingCostUsd + input.PackagingCostUsd + input.OnPlatformAdsCostUsd;
        var totalExpenses = directCosts + fees.TotalEtsyFeesUsd;
        var netProfit = totalRevenue - totalExpenses;

        var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100.0 : 0.0;
        var directCostBase = directCosts + fees.TotalEtsyFeesUsd;
        var roi = directCostBase > 0 ? (netProfit / directCostBase) * 100.0 : 0.0;

        var breakEvenPrice = CalculateBreakEvenPrice(input, directCosts);
        var recommendedOptimalPrice = CalculateOptimalPrice(input, directCosts, input.TargetProfitMarginPercent);

        var comparisonResult = CalculateOffsiteAdsComparison(input, totalRevenue, directCosts);

        var (healthRating, recommendations) = GenerateHealthReport(input, netProfit, profitMargin, fees, breakEvenPrice);

        return new ProfitabilityResult(
            Math.Round(totalRevenue, 2),
            Math.Round(totalExpenses, 2),
            fees,
            Math.Round(netProfit, 2),
            Math.Round(profitMargin, 1),
            Math.Round(roi, 1),
            Math.Round(breakEvenPrice, 2),
            Math.Round(recommendedOptimalPrice, 2),
            Math.Round(comparisonResult, 2),
            healthRating,
            recommendations);
    }

    /// <summary>
    /// Calculates exact itemized fee breakdown for a given total revenue.
    /// </summary>
    public static ItemizedFeeBreakdown CalculateFees(double totalRevenue, ProfitabilityInput input)
    {
        if (totalRevenue <= 0)
        {
            return new ItemizedFeeBreakdown(EtsyFeeRules.ListingFeeUsd, 0, 0, 0, 0, 0, EtsyFeeRules.ListingFeeUsd);
        }

        var listingFee = EtsyFeeRules.ListingFeeUsd;
        var transactionFee = totalRevenue * (EtsyFeeRules.TransactionFeePercent / 100.0);

        var (payPct, payFixed) = EtsyFeeRules.GetPaymentProcessingRate(input.SellerCountry);
        var paymentProcessingFee = (totalRevenue * (payPct / 100.0)) + payFixed;

        var regPct = EtsyFeeRules.GetRegulatoryFeePercent(input.SellerCountry);
        var regulatoryFee = totalRevenue * (regPct / 100.0);

        var offsiteFeePct = input.OffsiteAds switch
        {
            OffsiteAdsMode.Mandatory12Percent => EtsyFeeRules.OffsiteAdsMandatoryPercent,
            OffsiteAdsMode.Optional15Percent => EtsyFeeRules.OffsiteAdsOptionalPercent,
            _ => 0.0,
        };
        var rawOffsiteFee = totalRevenue * (offsiteFeePct / 100.0);
        var offsiteFee = Math.Min(rawOffsiteFee, EtsyFeeRules.OffsiteAdsCapUsd);

        var currencyFee = input.ApplyCurrencyConversion
            ? totalRevenue * (EtsyFeeRules.CurrencyConversionPercent / 100.0)
            : 0.0;

        var totalFees = listingFee + transactionFee + paymentProcessingFee + regulatoryFee + offsiteFee + currencyFee;

        return new ItemizedFeeBreakdown(
            Math.Round(listingFee, 2),
            Math.Round(transactionFee, 2),
            Math.Round(paymentProcessingFee, 2),
            Math.Round(regulatoryFee, 2),
            Math.Round(offsiteFee, 2),
            Math.Round(currencyFee, 2),
            Math.Round(totalFees, 2));
    }

    /// <summary>
    /// Calculates the minimum item price needed for Net Profit = 0.
    /// </summary>
    private static double CalculateBreakEvenPrice(ProfitabilityInput input, double directCosts)
    {
        // Numerical search for break-even item price between $0.50 and $10,000
        var low = 0.50;
        var high = 10000.0;

        for (var i = 0; i < 30; i++)
        {
            var mid = (low + high) / 2.0;
            var testRev = mid + input.BuyerShippingUsd + input.GiftWrapUsd;
            var testFees = CalculateFees(testRev, input);
            var testNet = testRev - (directCosts + testFees.TotalEtsyFeesUsd);

            if (testNet >= 0)
                high = mid;
            else
                low = mid;
        }

        return Math.Max(0.20, high);
    }

    /// <summary>
    /// Calculates the item price needed to achieve the target net profit margin %.
    /// </summary>
    private static double CalculateOptimalPrice(ProfitabilityInput input, double directCosts, double targetMarginPercent)
    {
        var targetMargin = Math.Clamp(targetMarginPercent, 5.0, 80.0) / 100.0;
        var low = 1.0;
        var high = 50000.0;

        for (var i = 0; i < 35; i++)
        {
            var mid = (low + high) / 2.0;
            var testRev = mid + input.BuyerShippingUsd + input.GiftWrapUsd;
            var testFees = CalculateFees(testRev, input);
            var testNet = testRev - (directCosts + testFees.TotalEtsyFeesUsd);
            var testMargin = testRev > 0 ? testNet / testRev : 0.0;

            if (testMargin >= targetMargin)
                high = mid;
            else
                low = mid;
        }

        return Math.Max(0.20, high);
    }

    private static double CalculateOffsiteAdsComparison(ProfitabilityInput input, double totalRevenue, double directCosts)
    {
        var compAdsMode = input.OffsiteAds == OffsiteAdsMode.Disabled
            ? OffsiteAdsMode.Optional15Percent
            : OffsiteAdsMode.Disabled;

        var compInput = input with { OffsiteAds = compAdsMode };
        var compFees = CalculateFees(totalRevenue, compInput);
        return totalRevenue - (directCosts + compFees.TotalEtsyFeesUsd);
    }

    private static (string Rating, List<string> Recommendations) GenerateHealthReport(
        ProfitabilityInput input,
        double netProfit,
        double profitMargin,
        ItemizedFeeBreakdown fees,
        double breakEvenPrice)
    {
        var recommendations = new List<string>();
        string rating;

        if (netProfit <= 0)
        {
            rating = "Zarar (Kritik)";
            recommendations.Add($"Ürün şu anki fiyatla zararda. Başabaş fiyatı en az ${breakEvenPrice:F2} olmalıdır.");
            recommendations.Add("Ürün fiyatını artırın veya tedarik/kargo maliyetlerini düşürün.");
        }
        else if (profitMargin < 15.0)
        {
            rating = "Düşük Kâr";
            recommendations.Add($"Kâr marjı %{profitMargin:F1} seviyesinde, düşük. İdeal Etsy hedefi %25-35 arasıdır.");
            recommendations.Add("Fiyatı bir miktar yükselterek kâr marjınızı koruyun.");
        }
        else if (profitMargin < 30.0)
        {
            rating = "İyi (Sağlıklı)";
            recommendations.Add($"Kâr marjı %{profitMargin:F1} ile sağlıklı seviyede.");
        }
        else
        {
            rating = "Mükemmel";
            recommendations.Add($"Kâr marjı %{profitMargin:F1} ile çok yüksek ve verimli.");
        }

        var regPct = EtsyFeeRules.GetRegulatoryFeePercent(input.SellerCountry);
        if (regPct > 0)
        {
            recommendations.Add($"{input.SellerCountry} satıcıları için %{regPct:F2} Yasal Düzenleme Ücreti (Regulatory Operating Fee) kesintiye dahildir.");
        }

        if (input.OffsiteAds != OffsiteAdsMode.Disabled)
        {
            var adsPct = input.OffsiteAds == OffsiteAdsMode.Mandatory12Percent ? 12 : 15;
            recommendations.Add($"Offsite Ads (%{adsPct}) aktif. Bu sipariş dış reklamla gelirse kesinti ${fees.OffsiteAdsFeeUsd:F2} olacaktır.");
        }

        if (input.ApplyCurrencyConversion)
        {
            recommendations.Add("İlan para birimi farkından dolayı %2.5 Para Birimi Dönüştürme ücreti eklendi.");
        }

        return (rating, recommendations);
    }
}
