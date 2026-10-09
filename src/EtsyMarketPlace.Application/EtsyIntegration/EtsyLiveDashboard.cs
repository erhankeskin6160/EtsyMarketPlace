using System.Globalization;
using System.Text.RegularExpressions;

namespace EtsyMarketPlace.Application.EtsyIntegration;

/// <summary>
/// Web kontrol paneli icin canli Etsy API verilerinden siparis bazli net kar ve
/// gunluk gelir/kar serisi hesaplar. Masaustu WinForms panelindeki
/// FinancialReportService.BuildOrderSummaries mantiginin sunucu tarafi portudur.
/// </summary>
public static class EtsyLiveDashboardCalculator
{
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    /// <summary>Siparis bazli satirlari hesaplar (masaustu BuildOrderSummaries portu).</summary>
    public static List<EtsyDashboardOrderRow> BuildOrderRows(
        IReadOnlyList<EtsyDashboardReceipt> receipts,
        IReadOnlyList<EtsyDashboardLedgerFee> ledgerFees,
        IReadOnlyDictionary<long, EtsyDashboardOrderCost> orderCosts,
        decimal exchangeRate = 49.16m)
    {
        var offsiteAdsMap = BuildOffsiteAdsMap(ledgerFees);
        var rows = new List<EtsyDashboardOrderRow>();

        foreach (var receipt in receipts)
        {
            if (!receipt.IsPaid && !receipt.IsCanceled) continue;

            var firstItem = receipt.Items.Count > 0 ? receipt.Items[0] : null;
            var title = string.IsNullOrWhiteSpace(firstItem?.Title) ? $"Sipariş #{receipt.ReceiptId}" : firstItem!.Title;
            var quantity = receipt.Items.Sum(i => i.Quantity);

            var grandTotal = receipt.GrandTotal;
            var subtotal = receipt.Subtotal;
            var shippingCost = receipt.ShippingCost;
            var discountAmt = receipt.DiscountAmt;
            var tax = receipt.TaxCost;
            var refundedAmt = receipt.RefundedAmount;

            if (receipt.CurrencyCode.Equals("TRY", StringComparison.OrdinalIgnoreCase) && exchangeRate > 0)
            {
                grandTotal /= exchangeRate;
                subtotal /= exchangeRate;
                shippingCost /= exchangeRate;
                discountAmt /= exchangeRate;
                tax /= exchangeRate;
                refundedAmt /= exchangeRate;
            }

            var originalGrandTotal = grandTotal;
            var isFullRefundOrCanceled = receipt.IsCanceled || (originalGrandTotal > 0 && refundedAmt >= originalGrandTotal);
            var isPartialRefund = !isFullRefundOrCanceled && refundedAmt > 0 && refundedAmt < originalGrandTotal;
            var isCanceled = isFullRefundOrCanceled;

            var etsyFees = 0m;
            var offsiteAdFee = 0m;
            var productCost = 0m;
            var hasCost = false;

            if (isCanceled)
            {
                grandTotal = 0m;
            }
            else
            {
                if (isPartialRefund)
                {
                    grandTotal = Math.Max(0, originalGrandTotal - refundedAmt);
                }

                // Subtotal bos gelirse GrandTotal'dan turet (islem komisyonunun %0 hesaplanmasini onler)
                if (subtotal <= 0 && grandTotal > 0)
                {
                    subtotal = Math.Max(0, grandTotal - shippingCost - tax + discountAmt);
                }

                var feeBase = subtotal > 0
                    ? (subtotal + shippingCost)
                    : Math.Max(0, grandTotal - tax);

                var transactionFee = Math.Round(feeBase * 0.065m, 2);
                var trPaymentFixedUsd = receipt.CurrencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase) || grandTotal > 0
                    ? 0.14m
                    : Math.Round(3m / exchangeRate, 2);
                var paymentFee = Math.Round(grandTotal * 0.065m, 2) + trPaymentFixedUsd;
                var regulatoryFee = Math.Round(feeBase * 0.0167m, 2);

                if (offsiteAdsMap.TryGetValue(receipt.ReceiptId, out var mappedOffsiteFee) && mappedOffsiteFee > 0)
                {
                    offsiteAdFee = mappedOffsiteFee;
                }
                else if (receipt.IsFromOffsiteAds)
                {
                    offsiteAdFee = Math.Round(feeBase * 0.15m, 2);
                }

                etsyFees = transactionFee + paymentFee + regulatoryFee + tax;

                if (orderCosts.TryGetValue(receipt.ReceiptId, out var orderCost))
                {
                    productCost = Math.Round(orderCost.ProductCost + orderCost.ShippingCost, 2);
                    hasCost = true;
                }
            }

            var netProfitUsd = isCanceled ? 0m : Math.Round(grandTotal - etsyFees - offsiteAdFee - productCost, 2);

            rows.Add(new EtsyDashboardOrderRow(
                receipt.ReceiptId,
                receipt.CreatedAt,
                title,
                quantity,
                grandTotal,
                netProfitUsd,
                Math.Round(netProfitUsd * exchangeRate, 2),
                hasCost,
                isCanceled));
        }

        return rows.OrderByDescending(r => r.CreatedAt).ToList();
    }

    /// <summary>Ayin gunluk brut satis / net kar serisini uretir (gun 1 .. bugun).</summary>
    public static EtsyDailySeries BuildDailySeries(
        IReadOnlyList<EtsyDashboardOrderRow> rows,
        int year,
        int month,
        DateTimeOffset nowUtc)
    {
        var nowLocal = nowUtc.ToOffset(TurkeyOffset);
        var isCurrentMonth = nowLocal.Year == year && nowLocal.Month == month;
        var lastDay = isCurrentMonth ? nowLocal.Day : DateTime.DaysInMonth(year, month);
        var labels = new string[lastDay];
        var gross = new decimal[lastDay];
        var net = new decimal[lastDay];

        var culture = CultureInfo.GetCultureInfo("tr-TR");
        for (var day = 1; day <= lastDay; day++)
        {
            labels[day - 1] = new DateTime(year, month, day).ToString("dd MMM", culture);
        }

        foreach (var row in rows)
        {
            var local = row.CreatedAt.ToOffset(TurkeyOffset);
            if (local.Year != year || local.Month != month) continue;
            if (local.Day < 1 || local.Day > lastDay) continue;

            gross[local.Day - 1] += row.GrandTotal;
            net[local.Day - 1] += row.NetProfitUsd;
        }

        for (var i = 0; i < lastDay; i++)
        {
            gross[i] = Math.Round(gross[i], 2);
            net[i] = Math.Round(net[i], 2);
        }

        return new EtsyDailySeries(labels, gross, net);
    }

    /// <summary>Ayin en cok ciro getiren urununu bulur (iptaller haric).</summary>
    public static (string? Title, decimal Revenue) PickTopProduct(IReadOnlyList<EtsyDashboardOrderRow> rows)
    {
        var best = rows
            .Where(r => !r.IsCanceled && !string.IsNullOrWhiteSpace(r.Title))
            .GroupBy(r => r.Title)
            .Select(g => (Title: g.Key, Revenue: Math.Round(g.Sum(x => x.GrandTotal), 2)))
            .OrderByDescending(x => x.Revenue)
            .FirstOrDefault();

        return best.Revenue > 0 ? (best.Title, best.Revenue) : (null, 0m);
    }

    /// <summary>Web kokpiti 'Siparişler & Net Kâr' tablosu için 15 sütunlu tam finans dökümü hesaplar (masaüstü FinancialReportService.BuildOrderSummaries paritesi).</summary>
    public static List<EtsyOrderFinancialDetail> BuildDetailedOrderRows(
        IReadOnlyList<EtsyDashboardReceipt> receipts,
        IReadOnlyList<EtsyDashboardLedgerFee> ledgerFees,
        IReadOnlyDictionary<long, EtsyDashboardOrderCost> orderCosts,
        decimal exchangeRate = 49.16m)
    {
        var offsiteAdsMap = BuildOffsiteAdsMap(ledgerFees);
        var rows = new List<EtsyOrderFinancialDetail>();

        foreach (var receipt in receipts)
        {
            if (!receipt.IsPaid && !receipt.IsCanceled) continue;

            var firstItem = receipt.Items.Count > 0 ? receipt.Items[0] : null;
            var title = string.IsNullOrWhiteSpace(firstItem?.Title) ? $"Sipariş #{receipt.ReceiptId}" : firstItem!.Title;
            var listingId = firstItem?.ListingId ?? 0L;
            var quantity = receipt.Items.Count > 0 ? receipt.Items.Sum(i => i.Quantity) : 1;
            var buyerName = string.IsNullOrWhiteSpace(receipt.BuyerName) ? "Misafir Müşteri" : receipt.BuyerName;

            var orderSpecificRate = HistoricalExchangeRateProvider.GetRateForDate(receipt.CreatedAt.UtcDateTime, exchangeRate);

            var grandTotal = receipt.GrandTotal;
            var subtotal = receipt.Subtotal;
            var shippingCost = receipt.ShippingCost;
            var discountAmt = receipt.DiscountAmt;
            var tax = receipt.TaxCost;
            var refundedAmt = receipt.RefundedAmount;

            if (receipt.CurrencyCode.Equals("TRY", StringComparison.OrdinalIgnoreCase) && orderSpecificRate > 0)
            {
                grandTotal /= orderSpecificRate;
                subtotal /= orderSpecificRate;
                shippingCost /= orderSpecificRate;
                discountAmt /= orderSpecificRate;
                tax /= orderSpecificRate;
                refundedAmt /= orderSpecificRate;
            }

            var originalGrandTotal = grandTotal;
            var isFullRefundOrCanceled = receipt.IsCanceled || (originalGrandTotal > 0 && refundedAmt >= originalGrandTotal);
            var isPartialRefund = !isFullRefundOrCanceled && refundedAmt > 0 && refundedAmt < originalGrandTotal;
            var isCanceled = isFullRefundOrCanceled;

            var orderStatus = isFullRefundOrCanceled ? "canceled" : (isPartialRefund ? "refunded" : "completed");
            var displayStatus = isFullRefundOrCanceled ? "İptal Edildi" : (isPartialRefund ? "Kısmi İade" : "Tamamlandı");

            var transactionFee = 0m;
            var paymentFee = 0m;
            var regulatoryFee = 0m;
            var etsyFees = 0m;
            var offsiteAdFee = 0m;
            decimal? productCost = null;
            var hasCost = false;

            if (isCanceled)
            {
                grandTotal = 0m;
                subtotal = 0m;
                shippingCost = 0m;
                discountAmt = 0m;
                tax = 0m;
            }
            else
            {
                if (isPartialRefund)
                {
                    grandTotal = Math.Max(0, originalGrandTotal - refundedAmt);
                }

                if (subtotal <= 0 && grandTotal > 0)
                {
                    subtotal = Math.Max(0, grandTotal - shippingCost - tax + discountAmt);
                }

                var feeBase = subtotal > 0
                    ? (subtotal + shippingCost)
                    : Math.Max(0, grandTotal - tax);

                transactionFee = Math.Round(feeBase * 0.065m, 2);
                var trPaymentFixedUsd = receipt.CurrencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase) || grandTotal > 0
                    ? 0.14m
                    : Math.Round(3m / orderSpecificRate, 2);
                paymentFee = Math.Round(grandTotal * 0.065m, 2) + trPaymentFixedUsd;
                regulatoryFee = Math.Round(feeBase * 0.0167m, 2);

                if (offsiteAdsMap.TryGetValue(receipt.ReceiptId, out var mappedOffsiteFee) && mappedOffsiteFee > 0)
                {
                    offsiteAdFee = mappedOffsiteFee;
                }
                else if (receipt.IsFromOffsiteAds)
                {
                    offsiteAdFee = Math.Round(feeBase * 0.15m, 2);
                }

                etsyFees = transactionFee + paymentFee + regulatoryFee + tax;

                if (orderCosts.TryGetValue(receipt.ReceiptId, out var orderCost))
                {
                    productCost = Math.Round(orderCost.ProductCost + orderCost.ShippingCost, 2);
                    hasCost = true;
                }
            }

            var netProfitUsd = isCanceled ? 0m : Math.Round(grandTotal - etsyFees - offsiteAdFee - (productCost ?? 0m), 2);
            var netProfitTry = Math.Round(netProfitUsd * orderSpecificRate, 2);
            var orderDate = receipt.CreatedAt.ToOffset(TurkeyOffset).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            rows.Add(new EtsyOrderFinancialDetail(
                receipt.ReceiptId,
                receipt.CreatedAt,
                orderDate,
                orderStatus,
                displayStatus,
                buyerName,
                title,
                listingId,
                quantity,
                Math.Round(grandTotal, 2),
                Math.Round(subtotal, 2),
                Math.Round(shippingCost, 2),
                Math.Round(discountAmt, 2),
                Math.Round(tax, 2),
                transactionFee,
                paymentFee,
                regulatoryFee,
                Math.Round(etsyFees, 2),
                offsiteAdFee,
                productCost,
                netProfitUsd,
                Math.Round(orderSpecificRate, 2),
                netProfitTry,
                hasCost,
                false,
                null));
        }

        return rows.OrderByDescending(r => r.CreatedAt).ToList();
    }

    /// <summary>Uzun urun basliklarini masaustu panelindeki gibi kisaltir.</summary>
    public static string ShortenTitle(string title, int maxLength = 60)
        => string.IsNullOrEmpty(title) || title.Length <= maxLength ? title : title[..(maxLength - 1)] + "…";

    private static Dictionary<long, decimal> BuildOffsiteAdsMap(IReadOnlyList<EtsyDashboardLedgerFee> entries)
    {
        var map = new Dictionary<long, decimal>();
        if (entries is null) return map;

        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Type, "offsite_ads", StringComparison.OrdinalIgnoreCase)) continue;

            var receiptId = entry.ReferenceId;
            if (receiptId <= 0 && !string.IsNullOrWhiteSpace(entry.Description))
            {
                var match = Regex.Match(entry.Description, @"#?(\d{9,11})");
                if (match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                {
                    receiptId = parsed;
                }
            }

            if (receiptId > 0)
            {
                map[receiptId] = Math.Abs(entry.Amount);
            }
        }

        return map;
    }
}

public sealed record EtsyDashboardReceiptItem(string Title, long ListingId, int Quantity);

public sealed record EtsyDashboardReceipt(
    long ReceiptId,
    DateTimeOffset CreatedAt,
    bool IsPaid,
    bool IsCanceled,
    bool IsFromOffsiteAds,
    string CurrencyCode,
    decimal GrandTotal,
    decimal Subtotal,
    decimal ShippingCost,
    decimal DiscountAmt,
    decimal TaxCost,
    decimal RefundedAmount,
    IReadOnlyList<EtsyDashboardReceiptItem> Items,
    string? BuyerName = null,
    long? BuyerUserId = null,
    string? BuyerEmail = null,
    string? Status = null);

public sealed record EtsyDashboardLedgerFee(string Type, long ReferenceId, string? Description, decimal Amount);

public sealed record EtsyDashboardOrderCost(decimal ProductCost, decimal ShippingCost);

public sealed record EtsyDashboardOrderRow(
    long ReceiptId,
    DateTimeOffset CreatedAt,
    string Title,
    int Quantity,
    decimal GrandTotal,
    decimal NetProfitUsd,
    decimal NetProfitTry,
    bool HasCostData,
    bool IsCanceled);

public sealed record EtsyOrderFinancialDetail(
    long ReceiptId,
    DateTimeOffset CreatedAt,
    string OrderDate,
    string OrderStatus,
    string DisplayStatus,
    string BuyerName,
    string ProductTitle,
    long ListingId,
    int Quantity,
    decimal GrandTotalUsd,
    decimal SubtotalUsd,
    decimal ShippingCostUsd,
    decimal DiscountAmtUsd,
    decimal TaxUsd,
    decimal TransactionFeeUsd,
    decimal PaymentFeeUsd,
    decimal RegulatoryFeeUsd,
    decimal EtsyFeesUsd,
    decimal OffsiteAdFeeUsd,
    decimal? ProductCostUsd,
    decimal NetProfitUsd,
    decimal ExchangeRate,
    decimal NetProfitTry,
    bool HasCostData,
    bool HasInvoice,
    string? InvoicePath = null);

public sealed record EtsyDailySeries(string[] Labels, decimal[] GrossSales, decimal[] NetProfit);
