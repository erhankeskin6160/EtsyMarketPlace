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
             raw.Contains("disburse") || rawRef.Contains("disburse") ||
             raw.Contains("transfer") || rawRef.Contains("transfer") ||
             desc.Contains("deposit") || desc.Contains("payout") || desc.Contains("disburse") ||
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
            var shipping = dayEntries.Where(e => e.Type == "shipping").Sum(e => e.Amount);
            var fees = dayEntries.Where(IsFeeType).Sum(e => e.Amount);

            var grossTry = dayEntries.Where(e => e.Type == "sale").Sum(e => e.AmountTry);
            var refundsTry = dayEntries.Where(e => e.Type == "refund").Sum(e => e.AmountTry);
            var innerAdsTry = dayEntries.Where(e => e.Type == "ad_fee").Sum(e => e.AmountTry);
            var offsiteAdsTry = dayEntries.Where(e => e.Type == "offsite_ads").Sum(e => e.AmountTry);
            var shippingTry = dayEntries.Where(e => e.Type == "shipping").Sum(e => e.AmountTry);
            var feesTry = dayEntries.Where(IsFeeType).Sum(e => e.AmountTry);

            var productCosts = productCostsByDay.TryGetValue(day, out var pc) ? pc : 0m;
            var dayRate = HistoricalExchangeRateProvider.GetRateForDate(day, fallbackExchangeRate);
            var productCostsTry = Math.Round(productCosts * dayRate, 2);

            var netRevenue = gross + refunds + fees + innerAds + offsiteAds + shipping;
            var netRevenueTry = grossTry + refundsTry + feesTry + innerAdsTry + offsiteAdsTry + shippingTry;
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

        var monthly = BuildMonthlySummaries(daily, entries);

        return new EtsyLedgerFinancialReport(
            totalGross, totalFees, totalInnerAds, totalOffsiteAds, totalRefunds, totalDeposits,
            totalShipping, totalProductCosts, totalNet, totalNet - totalProductCosts,
            totalGrossTry, totalFeesTry, totalInnerTry, totalOffsiteTry, totalRefundsTry,
            totalProductCostsTry, totalNetProfitTry, overallRate, daily, monthly);
    }

    /// <summary>
    /// Gunluk defter ozetlerinden ve defter kayitlarindan aylik donemsel ozetleri uretir.
    /// Her ayin toplami gunluklerin toplamina ve dolayisiyla ust KPI kartlarina kurusu kurusuna esittir.
    /// </summary>
    public static IReadOnlyList<EtsyLedgerMonthlySummary> BuildMonthlySummaries(
        IReadOnlyList<EtsyLedgerDailySummary> daily,
        IReadOnlyList<EtsyLedgerEntryDetail> entries)
    {
        var turkishMonthNames = new[]
        {
            "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        var monthly = new List<EtsyLedgerMonthlySummary>();

        var monthGroups = daily
            .GroupBy(d => (d.Date.Year, d.Date.Month))
            .OrderByDescending(g => g.Key.Year * 100 + g.Key.Month);

        foreach (var g in monthGroups)
        {
            var y = g.Key.Year;
            var m = g.Key.Month;
            var periodLabel = m >= 1 && m <= 12 ? $"{turkishMonthNames[m]} {y}" : $"{y}-{m:D2}";

            var orderCount = entries
                .Where(e => e.CreatedAt.UtcDateTime.Year == y && e.CreatedAt.UtcDateTime.Month == m && e.Type == "sale" && e.ReferenceId > 0)
                .Select(e => e.ReferenceId)
                .Distinct()
                .Count();

            if (orderCount == 0)
            {
                orderCount = entries.Count(e => e.CreatedAt.UtcDateTime.Year == y && e.CreatedAt.UtcDateTime.Month == m && e.Type == "sale");
            }

            var grossUsd = g.Sum(d => d.GrossSales);
            var grossTry = g.Sum(d => d.GrossSalesTry);
            var feesUsd = Math.Abs(g.Sum(d => d.EtsyFees));
            var feesTry = Math.Abs(g.Sum(d => d.EtsyFeesTry));
            var innerAdsTry = Math.Abs(g.Sum(d => d.InnerAdFeesTry));
            var offsiteAdsTry = Math.Abs(g.Sum(d => d.OffsiteAdFeesTry));
            var adFeesUsd = Math.Abs(g.Sum(d => d.InnerAdFees + d.OffsiteAdFees));
            var adFeesTry = innerAdsTry + offsiteAdsTry;
            var refundsUsd = Math.Abs(g.Sum(d => d.Refunds));
            var refundsTry = Math.Abs(g.Sum(d => d.RefundsTry));
            var productCostsUsd = g.Sum(d => d.ProductCosts);
            var productCostsTry = g.Sum(d => d.ProductCostsTry);
            var realProfitUsd = g.Sum(d => d.RealNetProfitUsd);
            var realProfitTry = g.Sum(d => d.RealNetProfitTry);

            var margin = grossTry > 0 ? Math.Round((realProfitTry / grossTry) * 100m, 1) : 0m;

            monthly.Add(new EtsyLedgerMonthlySummary(
                Year: y,
                Month: m,
                PeriodLabel: periodLabel,
                OrderCount: orderCount,
                GrossSalesUsd: Math.Round(grossUsd, 2),
                GrossSalesTry: Math.Round(grossTry, 2),
                EtsyFeesUsd: Math.Round(feesUsd, 2),
                EtsyFeesTry: Math.Round(feesTry, 2),
                AdFeesUsd: Math.Round(adFeesUsd, 2),
                AdFeesTry: Math.Round(adFeesTry, 2),
                InnerAdFeesTry: Math.Round(innerAdsTry, 2),
                OffsiteAdFeesTry: Math.Round(offsiteAdsTry, 2),
                RefundsUsd: Math.Round(refundsUsd, 2),
                RefundsTry: Math.Round(refundsTry, 2),
                ProductCostsUsd: Math.Round(productCostsUsd, 2),
                ProductCostsTry: Math.Round(productCostsTry, 2),
                BankPayoutsUsd: 0m,
                BankPayoutsTry: 0m,
                RealNetProfitUsd: Math.Round(realProfitUsd, 2),
                RealNetProfitTry: Math.Round(realProfitTry, 2),
                ProfitMarginPct: margin
            ));
        }

        return monthly;
    }

    /// <summary>
    /// Tekdüzen Hesap Planı (MSUGT 600–692) resmi gelir tablosu için defter kayıtlarının
    /// alt kırılımlarını üretir. Mevcut <see cref="BuildReport"/> formülünü değiştirmez;
    /// toplam komisyon, reklam, KDV/düzenleyici ücret ve iade tutarları birebir örtüşür.
    /// </summary>
    public static EtsyLedgerSubBreakdown BuildSubBreakdown(IReadOnlyList<EtsyLedgerEntryDetail> entries)
    {
        decimal grossUsd = 0m, grossRawTry = 0m;
        decimal txFeeUsd = 0m, txFeeTry = 0m;
        decimal listFeeUsd = 0m, listFeeTry = 0m;
        decimal procFeeUsd = 0m, procFeeTry = 0m;
        decimal regTaxFeeUsd = 0m, regTaxFeeTry = 0m;
        decimal otherFeeUsd = 0m, otherFeeTry = 0m;
        decimal innerAdUsd = 0m, innerAdTry = 0m;
        decimal offsiteAdUsd = 0m, offsiteAdTry = 0m;
        decimal refundUsd = 0m, refundTry = 0m;
        decimal shippingUsd = 0m, shippingTry = 0m;
        decimal depositUsd = 0m, depositTry = 0m;

        foreach (var e in entries)
        {
            switch (e.Type)
            {
                case "sale":
                    grossUsd += e.Amount;
                    grossRawTry += e.AmountTry;
                    break;
                case "refund":
                    refundUsd += -e.Amount;
                    refundTry += -e.AmountTry;
                    break;
                case "ad_fee":
                    innerAdUsd += -e.Amount;
                    innerAdTry += -e.AmountTry;
                    break;
                case "offsite_ads":
                    offsiteAdUsd += -e.Amount;
                    offsiteAdTry += -e.AmountTry;
                    break;
                case "shipping":
                    shippingUsd += -e.Amount;
                    shippingTry += -e.AmountTry;
                    break;
                case "deposit":
                    depositUsd += e.Amount;
                    depositTry += e.AmountTry;
                    break;
                case "transaction_fee":
                    txFeeUsd += -e.Amount;
                    txFeeTry += -e.AmountTry;
                    break;
                case "listing_fee":
                    listFeeUsd += -e.Amount;
                    listFeeTry += -e.AmountTry;
                    break;
                case "payment_processing":
                    procFeeUsd += -e.Amount;
                    procFeeTry += -e.AmountTry;
                    break;
                case "regulatory_operating_fee":
                case "etsy_tax_fee":
                    regTaxFeeUsd += -e.Amount;
                    regTaxFeeTry += -e.AmountTry;
                    break;
                default:
                    if (e.Amount < 0 || e.NetAmount < 0)
                    {
                        otherFeeUsd += -e.Amount;
                        otherFeeTry += -e.AmountTry;
                    }
                    break;
            }
        }

        return new EtsyLedgerSubBreakdown(
            GrossSalesUsd: grossUsd,
            GrossSalesRawTry: grossRawTry,
            TransactionFeeUsd: txFeeUsd,
            TransactionFeeTry: txFeeTry,
            ListingFeeUsd: listFeeUsd,
            ListingFeeTry: listFeeTry,
            PaymentProcessingUsd: procFeeUsd,
            PaymentProcessingTry: procFeeTry,
            RegulatoryAndTaxFeeUsd: regTaxFeeUsd,
            RegulatoryAndTaxFeeTry: regTaxFeeTry,
            OtherUnknownFeeUsd: otherFeeUsd,
            OtherUnknownFeeTry: otherFeeTry,
            InnerAdFeeUsd: innerAdUsd,
            InnerAdFeeTry: innerAdTry,
            OffsiteAdFeeUsd: offsiteAdUsd,
            OffsiteAdFeeTry: offsiteAdTry,
            RefundUsd: refundUsd,
            RefundTry: refundTry,
            ShippingLedgerNetUsd: shippingUsd,
            ShippingLedgerNetTry: shippingTry,
            DepositUsd: depositUsd,
            DepositTry: depositTry);
    }
}

/// <summary>Resmi MSUGT Gelir Tablosu için defter alt kırılımları (giderler net pozitif tutar).</summary>
public sealed record EtsyLedgerSubBreakdown(
    decimal GrossSalesUsd,
    decimal GrossSalesRawTry,
    decimal TransactionFeeUsd,
    decimal TransactionFeeTry,
    decimal ListingFeeUsd,
    decimal ListingFeeTry,
    decimal PaymentProcessingUsd,
    decimal PaymentProcessingTry,
    decimal RegulatoryAndTaxFeeUsd,
    decimal RegulatoryAndTaxFeeTry,
    decimal OtherUnknownFeeUsd,
    decimal OtherUnknownFeeTry,
    decimal InnerAdFeeUsd,
    decimal InnerAdFeeTry,
    decimal OffsiteAdFeeUsd,
    decimal OffsiteAdFeeTry,
    decimal RefundUsd,
    decimal RefundTry,
    decimal ShippingLedgerNetUsd,
    decimal ShippingLedgerNetTry,
    decimal DepositUsd,
    decimal DepositTry);

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
    IReadOnlyList<EtsyLedgerDailySummary> Daily,
    IReadOnlyList<EtsyLedgerMonthlySummary>? Monthly = null);

/// <summary>Aylik finansal ozet (masaustu donemsel muhasebe portu, USD/TRY).</summary>
public sealed record EtsyLedgerMonthlySummary(
    int Year,
    int Month,
    string PeriodLabel,
    int OrderCount,
    decimal GrossSalesUsd,
    decimal GrossSalesTry,
    decimal EtsyFeesUsd,
    decimal EtsyFeesTry,
    decimal AdFeesUsd,
    decimal AdFeesTry,
    decimal InnerAdFeesTry,
    decimal OffsiteAdFeesTry,
    decimal RefundsUsd,
    decimal RefundsTry,
    decimal ProductCostsUsd,
    decimal ProductCostsTry,
    decimal BankPayoutsUsd,
    decimal BankPayoutsTry,
    decimal RealNetProfitUsd,
    decimal RealNetProfitTry,
    decimal ProfitMarginPct);
