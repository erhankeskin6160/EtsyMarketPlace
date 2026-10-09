using System.Globalization;

namespace EtsyMarketPlace.Application.EtsyIntegration;

/// <summary>
/// VDS sunucu finans motoru - Etsy odeme hesabi defter kayitlarini, masaustu WinForms
/// uygulamasindaki Services\FinancialReportService.cs mantiginin BIREBIR portuyla
/// siniflandirir ve gunluk/donemsel toplamlara cevirir.
///
/// PARITE SOZLESMESI (bkz. docs/finans-motoru-ve-parite.md):
///  - Siniflandirma sirasi masaustu MapEntry ile aynidir:
///    deposit -> offsite -> ad_fee -> shipping -> listing -> transaction -> processing ->
///    vat/tax -> refund -> sale -> (ham tur adi).
///  - Net formulu: netRevenue = gross + refunds + fees + innerAds + offsiteAds (+ shipping);
///    gercek net kar = netRevenue - urun maliyetleri. Tutarlar isaretli tutulur (giderler negatif).
///  - Referans degerler (masaustu paritesi): 2026-10-01 brut 35,5742 / komisyon 5,9898 /
///    dis reklam 5,4729 / maliyet 10,42 / net 13,6915. Bu motor bu degerleri birebir uretmelidir.
/// </summary>
public static class EtsyLedgerFinancialEngine
{
    /// <summary>Masaustu MapEntry siniflandirmasinin birebir portu (lowercase alanlar beklenir).</summary>
    public static string ClassifyEntry(string rawType, string rawReferenceType, string lowerDescription)
    {
        var raw = rawType ?? string.Empty;
        var rawRef = rawReferenceType ?? string.Empty;
        var desc = (lowerDescription ?? string.Empty).ToLowerInvariant();

        // 1) Etsy banka transferi / depozito (ucret degilse)
        if ((raw.Contains("deposit") || rawRef.Contains("deposit") ||
             raw.Contains("payout") || rawRef.Contains("payout") ||
             raw.Contains("disbursement") || rawRef.Contains("disbursement") ||
             raw.Contains("transfer") || rawRef.Contains("transfer") ||
             desc.Contains("deposit") || desc.Contains("payout") || desc.Contains("disbursement") ||
             desc.Contains("transfer") || desc.Contains("yatırılan") || desc.Contains("banka") || desc.Contains("hesaba"))
            && !raw.Contains("fee") && !rawRef.Contains("fee") && !desc.Contains("fee"))
        {
            return "deposit";
        }

        // 2) Dis reklam (Offsite Ads)
        if (raw.Contains("offsite") || rawRef.Contains("offsite") || desc.Contains("offsite"))
        {
            return "offsite_ads";
        }

        // 3) Ic reklam (Etsy Ads / Promoted / Search / Marketing / ATS)
        if (raw == "ad_fee" || raw == "ads" || rawRef == "ad_fee" || rawRef == "ads" ||
            raw.Contains("ats") || rawRef.Contains("ats") ||
            raw.Contains("prolist") || rawRef.Contains("prolist") ||
            raw.Contains("promoted") || rawRef.Contains("promoted") ||
            raw.Contains("marketing") || rawRef.Contains("marketing") ||
            desc.Contains("etsy ads") || desc.Contains("reklam") || desc.Contains("promoted") || desc.Contains("marketing"))
        {
            return "ad_fee";
        }

        // 4) Kargo
        if (raw.Contains("shipping") || rawRef.Contains("shipping") || desc.Contains("shipping"))
        {
            return "shipping";
        }

        // 5) Ilan / listeleme ucreti
        if (raw.Contains("listing") || rawRef.Contains("listing") || desc.Contains("listing_fee"))
        {
            return "listing_fee";
        }

        // 6) Islem komisyonu
        if (raw.Contains("transaction") || rawRef.Contains("transaction") || desc.Contains("transaction_fee"))
        {
            return "transaction_fee";
        }

        // 7) Odeme isleme / banka ucretleri
        if (raw.Contains("processing") || rawRef.Contains("processing") ||
            desc.Contains("processing_fee") || desc.Contains("deposit fee") || desc.Contains("deposit_fee"))
        {
            return "payment_processing";
        }

        // 8) KDV / vergi / kurumsal ucretler
        if (raw.Contains("vat") || rawRef.Contains("vat") ||
            raw.Contains("regulatory") || rawRef.Contains("regulatory") ||
            raw.Contains("setup") || rawRef.Contains("setup") ||
            raw.Contains("subscription") || rawRef.Contains("subscription") ||
            desc.Contains("vat") || desc.Contains("kdv") || desc.Contains("operating fee") || desc.Contains("tax"))
        {
            return "etsy_tax_fee";
        }

        // 9) Iade / iptal
        if (raw.Contains("refund") || rawRef.Contains("refund") ||
            raw.Contains("cancel") || rawRef.Contains("cancel") ||
            desc.Contains("refund") || desc.Contains("iade") || desc.Contains("cancel"))
        {
            return "refund";
        }

        // 10) Satis kayitlari
        if (raw.Contains("sale") || rawRef.Contains("sale") ||
            (raw.Contains("payment") && !raw.Contains("processing")) ||
            desc.Contains("sale") || desc.Contains("sipariş"))
        {
            return "sale";
        }

        // 11) Bilinmeyen tur: ham tur adi korunur (negatifse toplamlarda ucret sayilir)
        return raw.Length > 0 ? raw : (rawRef.Length > 0 ? rawRef : "other");
    }

    private static bool IsFeeType(EtsyLedgerEntryDetail e) =>
        e.Type is "listing_fee" or "transaction_fee" or "payment_processing"
            or "regulatory_operating_fee" or "etsy_tax_fee"
        || (e.Type is not ("sale" or "refund" or "ad_fee" or "offsite_ads" or "deposit" or "shipping")
            && (e.Amount < 0 || e.NetAmount < 0));

    /// <summary>
    /// Kayitlardan toplam ve gunluk ozetleri uretir (masaustu BuildReportInternalAsync +
    /// BuildPeriodSummaries portu). Tutarlar isaretli: giderler negatiftir.
    /// </summary>
    public static EtsyLedgerFinancialReport BuildReport(
        IReadOnlyList<EtsyLedgerEntryDetail> entries,
        IReadOnlyDictionary<DateTime, decimal> productCostsByDay,
        decimal fallbackExchangeRate = 48.25m)
    {
        decimal totalGross = 0, totalRefunds = 0, totalFees = 0, totalInnerAds = 0,
                totalOffsiteAds = 0, totalDeposits = 0, totalShipping = 0;

        foreach (var e in entries)
        {
            switch (e.Type)
            {
                case "sale":
                    totalGross += e.Amount;
                    break;
                case "refund":
                    totalRefunds += e.Amount;
                    break;
                case "ad_fee":
                    totalInnerAds += e.Amount;
                    break;
                case "offsite_ads":
                    totalOffsiteAds += e.Amount;
                    break;
                case "deposit":
                    totalDeposits += e.Amount;
                    break;
                case "shipping":
                    totalShipping += e.Amount;
                    break;
                case "listing_fee":
                case "transaction_fee":
                case "payment_processing":
                case "regulatory_operating_fee":
                case "etsy_tax_fee":
                    totalFees += e.Amount;
                    break;
                default:
                    if (e.Amount < 0 || e.NetAmount < 0)
                    {
                        totalFees += e.Amount;
                    }
                    break;
            }
        }

        var totalProductCosts = productCostsByDay.Values.Sum();
        var totalNet = totalGross + totalRefunds + totalFees + totalInnerAds + totalOffsiteAds + totalShipping;

        // Gunluk ozetler (UTC gun; masaustu ile ayni bolumleme)
        var daily = new List<EtsyLedgerDailySummary>();
        var entryDays = entries.GroupBy(e => e.CreatedAt.UtcDateTime.Date).ToDictionary(g => g.Key, g => g.ToList());
        var allDays = entryDays.Keys.Union(productCostsByDay.Keys).OrderBy(d => d).ToList();

        foreach (var day in allDays)
        {
            var dayEntries = entryDays.TryGetValue(day, out var list) ? list : new List<EtsyLedgerEntryDetail>();

            var gross = dayEntries.Where(e => e.Type == "sale").Sum(e => e.Amount);
            var refunds = dayEntries.Where(e => e.Type == "refund").Sum(e => e.Amount);
            var innerAds = dayEntries.Where(e => e.Type == "ad_fee").Sum(e => e.Amount);
            var offsiteAds = dayEntries.Where(e => e.Type == "offsite_ads").Sum(e => e.Amount);
            var deposits = dayEntries.Where(e => e.Type == "deposit").Sum(e => e.Amount);
            var fees = dayEntries.Where(IsFeeType).Sum(e => e.Amount);

            var grossTry = dayEntries.Where(e => e.Type == "sale").Sum(e => e.AmountTry);
            var refundsTry = dayEntries.Where(e => e.Type == "refund").Sum(e => e.AmountTry);
            var innerAdsTry = dayEntries.Where(e => e.Type == "ad_fee").Sum(e => e.AmountTry);
            var offsiteAdsTry = dayEntries.Where(e => e.Type == "offsite_ads").Sum(e => e.AmountTry);
            var feesTry = dayEntries.Where(IsFeeType).Sum(e => e.AmountTry);

            var productCosts = productCostsByDay.TryGetValue(day, out var pc) ? pc : 0m;
            var dayRate = HistoricalExchangeRateProvider.GetRateForDate(day, fallbackExchangeRate);
            var productCostsTry = Math.Round(productCosts * dayRate, 2);

            var netRevenue = gross + refunds + fees + innerAds + offsiteAds;
            var netRevenueTry = grossTry + refundsTry + feesTry + innerAdsTry + offsiteAdsTry;
            var realProfit = netRevenue - productCosts;
            var realProfitTry = netRevenueTry - productCostsTry;

            var avgRate = gross > 0
                ? Math.Round(grossTry / gross, 4)
                : (dayEntries.Count > 0
                    ? Math.Round(dayEntries.Average(e => e.ExchangeRate), 4)
                    : fallbackExchangeRate);

            daily.Add(new EtsyLedgerDailySummary(
                day, gross, fees, innerAds, offsiteAds, refunds, deposits, productCosts,
                netRevenue, realProfit, grossTry, feesTry, innerAdsTry, offsiteAdsTry,
                refundsTry, productCostsTry, netRevenueTry, realProfitTry, avgRate));
        }

        var totalGrossTry = daily.Sum(d => Math.Round(d.GrossSales * d.AverageExchangeRate, 2));
        var totalFeesTry = daily.Sum(d => d.EtsyFeesTry);
        var totalInnerTry = daily.Sum(d => d.InnerAdFeesTry);
        var totalOffsiteTry = daily.Sum(d => d.OffsiteAdFeesTry);
        var totalRefundsTry = daily.Sum(d => d.RefundsTry);
        var totalNetProfitTry = daily.Sum(d => d.RealNetProfitTry);
        var totalProductCostsTry = daily.Sum(d => d.ProductCostsTry);
        var overallRate = totalGross > 0 ? Math.Round(totalGrossTry / totalGross, 4) : fallbackExchangeRate;

        return new EtsyLedgerFinancialReport(
            totalGross, totalFees, totalInnerAds, totalOffsiteAds, totalRefunds, totalDeposits,
            totalShipping, totalProductCosts, totalNet, totalNet - totalProductCosts,
            totalGrossTry, totalFeesTry, totalInnerTry, totalOffsiteTry, totalRefundsTry,
            totalProductCostsTry, totalNetProfitTry, overallRate, daily);
    }
}

/// <summary>Sunucu finans motoru icin tam alanli defter kaydi (tutarlar USD'ye cevrilmis).</summary>
public sealed record EtsyLedgerEntryDetail(
    string Type,
    string RawType,
    string RawReferenceType,
    string Description,
    decimal Amount,
    decimal NetAmount,
    decimal AmountTry,
    string Currency,
    DateTimeOffset CreatedAt,
    decimal ExchangeRate,
    long ReferenceId,
    long EntryId);

/// <summary>Gunluk finansal ozet (masaustu PeriodFinancialSummary portu).</summary>
public sealed record EtsyLedgerDailySummary(
    DateTime Date,
    decimal GrossSales,
    decimal EtsyFees,
    decimal InnerAdFees,
    decimal OffsiteAdFees,
    decimal Refunds,
    decimal Deposits,
    decimal ProductCosts,
    decimal EtsyNetRevenue,
    decimal RealNetProfitUsd,
    decimal GrossSalesTry,
    decimal EtsyFeesTry,
    decimal InnerAdFeesTry,
    decimal OffsiteAdFeesTry,
    decimal RefundsTry,
    decimal ProductCostsTry,
    decimal EtsyNetRevenueTry,
    decimal RealNetProfitTry,
    decimal AverageExchangeRate);

/// <summary>Donem toplamlari + gunluk ozetler (masaustu FinancialReport portu, USD/TRY).</summary>
public sealed record EtsyLedgerFinancialReport(
    decimal GrossSales,
    decimal EtsyFees,
    decimal InnerAdFees,
    decimal OffsiteAdFees,
    decimal Refunds,
    decimal Deposits,
    decimal ShippingCredits,
    decimal ProductCosts,
    decimal EtsyNetRevenue,
    decimal RealNetProfitUsd,
    decimal GrossSalesTry,
    decimal EtsyFeesTry,
    decimal InnerAdFeesTry,
    decimal OffsiteAdFeesTry,
    decimal RefundsTry,
    decimal ProductCostsTry,
    decimal NetProfitTry,
    decimal AverageExchangeRate,
    IReadOnlyList<EtsyLedgerDailySummary> Daily);
