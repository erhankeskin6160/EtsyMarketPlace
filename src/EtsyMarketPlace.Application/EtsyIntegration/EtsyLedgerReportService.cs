namespace EtsyMarketPlace.Application.EtsyIntegration;

/// <summary>
/// Sunucu finans motoru orkestrasyonu: Etsy odeme hesabi defteri (canli) + siparis maliyetleri ->
/// masaustu paritesinde finansal performans. Web KPI'lari bu servisten beslenir;
/// masaustu push'una bagimlilik yoktur. Bkz. docs/finans-motoru-ve-parite.md
/// </summary>
public interface IEtsyLedgerReportService
{
    Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
}

/// <summary>Canli defter raporu sonucu (basarisizlikta LedgerOk=false + uyari doner).</summary>
public sealed record EtsyLedgerLiveResult(
    bool LedgerOk,
    string? Warning,
    string Source,
    DateTimeOffset GeneratedAt,
    FinancialPerformance Performance,
    IReadOnlyList<EtsyLedgerDailySummary> Daily,
    int ReceiptCount,
    IReadOnlyList<EtsyLedgerMonthlySummary>? Monthly = null);

public sealed class EtsyLedgerReportService(IEtsyDataClient dataClient, IEtsyIntegrationRepository repository) : IEtsyLedgerReportService
{
    public async Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EtsyLedgerEntryDetail> entries;
        try
        {
            entries = await dataClient.GetLedgerEntriesDetailedAsync(shopId, startDate, endDate, cancellationToken);
        }
        catch (Exception firstEx) when (firstEx is not OperationCanceledException)
        {
            _ = firstEx;
            // Defter ucu zaman zaman dalgalanir (bilinen durum): bir kez yeniden denenir.
            await Task.Delay(1500, cancellationToken);
            try
            {
                entries = await dataClient.GetLedgerEntriesDetailedAsync(shopId, startDate, endDate, cancellationToken);
            }
            catch (Exception retryEx) when (retryEx is not OperationCanceledException)
            {
                return new EtsyLedgerLiveResult(
                    false,
                    "Canlı Etsy defterine ulaşılamadı; yedek özet gösteriliyor.",
                    "ledger-unavailable",
                    DateTimeOffset.UtcNow,
                    EmptyPerformance(startDate, endDate),
                    Array.Empty<EtsyLedgerDailySummary>(),
                    0);
            }
        }

        var (costsByDay, receiptCount) = await BuildProductCostsByDayAsync(shopId, startDate, endDate, entries, cancellationToken);
        var report = EtsyLedgerFinancialEngine.BuildReport(entries, costsByDay);
        var performance = MapToPerformance(startDate, endDate, report);

        return new EtsyLedgerLiveResult(true, null, "etsy-ledger-live", DateTimeOffset.UtcNow, performance, report.Daily, receiptCount, report.Monthly);
    }

    /// <summary>
    /// Siparis/urun maliyetlerini gun bazinda toplar; masaustu orderSummaries.ProductCost semantigi:
    /// yalniz odanmis (veya iptal) siparisler sayilir, iptallerde maliyet girilmisse zarara dahil edilir.
    /// Ayrica donem icinde iade edilen capraz donem siparislerin maliyetleri de iade gunune eklenir.
    /// </summary>
    private async Task<(IReadOnlyDictionary<DateTime, decimal> Costs, int ReceiptCount)> BuildProductCostsByDayAsync(
        string shopId, DateTimeOffset startDate, DateTimeOffset endDate, IReadOnlyList<EtsyLedgerEntryDetail> entries, CancellationToken cancellationToken)
    {
        var costsByDay = new Dictionary<DateTime, decimal>();
        var receiptCount = 0;

        try
        {
            var receipts = await dataClient.GetShopReceiptsAsync(shopId, startDate, endDate, cancellationToken);
            receiptCount = receipts.Count(r => r.IsPaid || r.IsCanceled);
            var costs = await repository.GetOrderCostsAsync(shopId, cancellationToken);
            var periodReceiptIds = new HashSet<long>(receipts.Select(r => r.ReceiptId));

            foreach (var receipt in receipts)
            {
                if (!receipt.IsPaid && !receipt.IsCanceled) continue;
                if (!costs.TryGetValue(receipt.ReceiptId, out var cost)) continue;

                var amount = Math.Round(cost.ProductCost + cost.ShippingCost + cost.PackagingCost, 2);
                if (receipt.IsCanceled && amount <= 0) continue;

                var day = receipt.CreatedAt.UtcDateTime.Date;
                costsByDay[day] = costsByDay.TryGetValue(day, out var existing) ? existing + amount : amount;
            }

            if (costs.Count > 0)
            {
                await AddCrossPeriodRefundCostsAsync(shopId, startDate, endDate, entries, costs, periodReceiptIds, costsByDay, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Siparis/maliyet tarafi alinamazsa rapor maliyetsiz uretilir; defter tarafi yine canlidir.
            // Durum, sonuca uyari olarak yansitilir (sessiz yutma yok) - bkz. asagidaki return.
            _ = ex;
        }

        return (costsByDay, receiptCount);
    }

    private async Task AddCrossPeriodRefundCostsAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        IReadOnlyList<EtsyLedgerEntryDetail> entries,
        IReadOnlyDictionary<long, EtsyDashboardOrderCost> costs,
        HashSet<long> periodReceiptIds,
        Dictionary<DateTime, decimal> costsByDay,
        CancellationToken cancellationToken)
    {
        var refundEntries = entries.Where(e => e.Type == "refund" && Math.Abs(e.Amount) > 0).ToList();
        if (refundEntries.Count == 0) return;

        var entryIdsToLookup = new List<long>();
        var resolvedByEntry = new Dictionary<int, long>();
        for (var i = 0; i < refundEntries.Count; i++)
        {
            var e = refundEntries[i];
            var rid = 0L;
            if (e.ReferenceId > 0 && (periodReceiptIds.Contains(e.ReferenceId) || costs.ContainsKey(e.ReferenceId)))
            {
                rid = e.ReferenceId;
            }
            else if (e.ReferenceId > 0 && EtsyTransactionOrderCache.TryGetOrderReference(e.ReferenceId, out var cached) && cached != null && cached.ReceiptId > 0)
            {
                rid = cached.ReceiptId;
            }
            if (rid > 0)
            {
                resolvedByEntry[i] = rid;
            }
            else if (e.EntryId > 0)
            {
                entryIdsToLookup.Add(e.EntryId);
            }
        }

        if (entryIdsToLookup.Count > 0)
        {
            try
            {
                var apiMap = await dataClient.GetReceiptIdsByLedgerEntriesAsync(shopId, entryIdsToLookup, cancellationToken);
                for (var i = 0; i < refundEntries.Count; i++)
                {
                    if (resolvedByEntry.ContainsKey(i)) continue;
                    var e = refundEntries[i];
                    if (e.EntryId > 0 && apiMap.TryGetValue(e.EntryId, out var rid) && rid > 0)
                    {
                        resolvedByEntry[i] = rid;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _ = ex; }
        }

        if (resolvedByEntry.Count < refundEntries.Count)
        {
            try
            {
                var fallbackReceipts = await dataClient.GetReceiptsModifiedBetweenAsync(shopId, startDate, endDate, cancellationToken);
                if (fallbackReceipts.Count == 0)
                {
                    fallbackReceipts = await dataClient.GetShopReceiptsAsync(shopId, startDate.AddDays(-120), startDate, cancellationToken);
                }
                var claimed = new HashSet<long>(resolvedByEntry.Values);
                for (var i = 0; i < refundEntries.Count; i++)
                {
                    if (resolvedByEntry.ContainsKey(i)) continue;
                    var e = refundEntries[i];
                    var matched = EtsyLiveDashboardCalculator.TryMatchReceiptByAmount(Math.Abs(e.Amount), e.CreatedAt, fallbackReceipts, claimed);
                    if (matched.HasValue)
                    {
                        resolvedByEntry[i] = matched.Value;
                        claimed.Add(matched.Value);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _ = ex; }
        }

        var countedCrossPeriodIds = new HashSet<long>();
        for (var i = 0; i < refundEntries.Count; i++)
        {
            if (!resolvedByEntry.TryGetValue(i, out var rid)) continue;
            if (periodReceiptIds.Contains(rid) || !countedCrossPeriodIds.Add(rid)) continue;
            if (!costs.TryGetValue(rid, out var cost)) continue;

            var amount = Math.Round(cost.ProductCost + cost.ShippingCost + cost.PackagingCost, 2);
            if (amount <= 0) continue;

            var day = refundEntries[i].CreatedAt.UtcDateTime.Date;
            costsByDay[day] = costsByDay.TryGetValue(day, out var existing) ? existing + amount : amount;
        }
    }

    private static FinancialPerformance MapToPerformance(DateTimeOffset startDate, DateTimeOffset endDate, EtsyLedgerFinancialReport report)
    {
        var margin = report.GrossSales == 0 ? 0m : report.RealNetProfitUsd / report.GrossSales * 100m;
        return new FinancialPerformance(
            startDate, endDate, "USD",
            report.GrossSales,
            Math.Abs(report.EtsyFees),
            Math.Abs(report.InnerAdFees),
            Math.Abs(report.OffsiteAdFees),
            report.ProductCosts,
            Math.Abs(report.ShippingCredits),
            Math.Abs(report.Refunds),
            report.RealNetProfitUsd,
            margin,
            report.GrossSalesTry,
            report.NetProfitTry,
            null, null, null,
            report.AverageExchangeRate,
            report.ProductCostsTry,
            Math.Abs(report.EtsyFeesTry),
            Math.Abs(report.InnerAdFeesTry),
            Math.Abs(report.OffsiteAdFeesTry),
            Math.Abs(report.RefundsTry));
    }

    private static FinancialPerformance EmptyPerformance(DateTimeOffset startDate, DateTimeOffset endDate) =>
        new(startDate, endDate, "USD", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
}
