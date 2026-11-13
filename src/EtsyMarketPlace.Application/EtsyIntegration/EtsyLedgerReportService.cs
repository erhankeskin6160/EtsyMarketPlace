namespace EtsyMarketPlace.Application.EtsyIntegration;

/// <summary>
/// Sunucu finans motoru orkestrasyonu: Etsy odeme hesabi defteri (canli) + siparis maliyetleri ->
/// masaustu paritesinde finansal performans. Web KPI'lari bu servisten beslenir;
/// masaustu push'una bagimlilik yoktur. Bkz. docs/finans-motoru-ve-parite.md
/// </summary>
public interface IEtsyLedgerReportService
{
    Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resmi MSUGT Gelir Tablosu (600–692) için canlı defter alt kırılımları ile aktif vs iptal/iade sipariş maliyet ayrımını döner.
    /// Varsayılan implementasyon test stub'larıyla geriye dönük uyumluluk için null döner.
    /// </summary>
    Task<EtsyAccountingLedgerData?> GetAccountingLedgerDataAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<EtsyAccountingLedgerData?>(null);
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

/// <summary>Aktif ve iade/iptal sipariş maliyetlerinin TDHP hesaplarına (620 / 760-631 / 689) dağıtım kırılımı.</summary>
public sealed record EtsyOrderCostAccountSplit(
    decimal ActiveProductCostUsd,
    decimal ActiveProductCostTry,
    decimal ActiveShippingCostUsd,
    decimal ActiveShippingCostTry,
    decimal ActivePackagingCostUsd,
    decimal ActivePackagingCostTry,
    decimal CanceledProductCostUsd,
    decimal CanceledProductCostTry,
    decimal CanceledShippingCostUsd,
    decimal CanceledShippingCostTry,
    decimal CanceledPackagingCostUsd,
    decimal CanceledPackagingCostTry,
    int ActiveCostedOrderCount,
    int CanceledOrRefundedCostedOrderCount,
    IReadOnlyList<EtsyOrderCostAccountingLine> OrderCostLines)
{
    public decimal ActiveTotalUsd => ActiveProductCostUsd + ActiveShippingCostUsd + ActivePackagingCostUsd;
    public decimal ActiveTotalTry => ActiveProductCostTry + ActiveShippingCostTry + ActivePackagingCostTry;
    public decimal CanceledTotalUsd => CanceledProductCostUsd + CanceledShippingCostUsd + CanceledPackagingCostUsd;
    public decimal CanceledTotalTry => CanceledProductCostTry + CanceledShippingCostTry + CanceledPackagingCostTry;
    public decimal TotalUsd => ActiveTotalUsd + CanceledTotalUsd;
    public decimal TotalTry => ActiveTotalTry + CanceledTotalTry;

    public static readonly EtsyOrderCostAccountSplit Empty = new(
        0m, 0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0m, 0m, 0m, 0m,
        0, 0, Array.Empty<EtsyOrderCostAccountingLine>());
}

/// <summary>Sipariş bazlı maliyet kaydı (gelir tablosu satır detayı / mutabakat için).</summary>
public sealed record EtsyOrderCostAccountingLine(
    long ReceiptId,
    string BuyerName,
    DateTimeOffset Date,
    bool IsCanceledOrRefunded,
    bool IsCrossPeriodRefund,
    decimal ProductCostUsd,
    decimal ShippingCostUsd,
    decimal PackagingCostUsd,
    decimal TotalCostUsd,
    decimal TotalCostTry,
    decimal ExchangeRate);

/// <summary>Resmi MSUGT Gelir Tablosu handler'ı için tam canlı Etsy defter + sipariş maliyet paketi.</summary>
public sealed record EtsyAccountingLedgerData(
    bool LedgerOk,
    string? Warning,
    DateTimeOffset GeneratedAt,
    EtsyLedgerFinancialReport Report,
    EtsyLedgerSubBreakdown SubBreakdown,
    EtsyOrderCostAccountSplit CostSplit,
    IReadOnlyList<EtsyDashboardReceipt> Receipts,
    IReadOnlyList<EtsyLedgerEntryDetail> Entries);

public sealed class EtsyLedgerReportService(IEtsyDataClient dataClient, IEtsyIntegrationRepository repository) : IEtsyLedgerReportService
{
    private const decimal DefaultFallbackExchangeRate = 48.25m;

    public async Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
    {
        var (ledgerOk, warning, entries) = await FetchEntriesWithRetryAsync(shopId, startDate, endDate, cancellationToken);
        if (!ledgerOk)
        {
            return new EtsyLedgerLiveResult(
                false,
                warning,
                "ledger-unavailable",
                DateTimeOffset.UtcNow,
                EmptyPerformance(startDate, endDate),
                Array.Empty<EtsyLedgerDailySummary>(),
                0);
        }

        var resolution = await ResolveCostsAsync(shopId, startDate, endDate, entries, cancellationToken);
        var report = EtsyLedgerFinancialEngine.BuildReport(entries, resolution.CostsByDay);
        var performance = MapToPerformance(startDate, endDate, report);

        return new EtsyLedgerLiveResult(true, null, "etsy-ledger-live", DateTimeOffset.UtcNow, performance, report.Daily, resolution.ReceiptCount, report.Monthly);
    }

    public async Task<EtsyAccountingLedgerData?> GetAccountingLedgerDataAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var (ledgerOk, warning, entries) = await FetchEntriesWithRetryAsync(shopId, startDate, endDate, cancellationToken);
        if (!ledgerOk)
        {
            var emptyReport = EtsyLedgerFinancialEngine.BuildReport(Array.Empty<EtsyLedgerEntryDetail>(), new Dictionary<DateTime, decimal>());
            var emptySub = EtsyLedgerFinancialEngine.BuildSubBreakdown(Array.Empty<EtsyLedgerEntryDetail>());
            return new EtsyAccountingLedgerData(
                false,
                warning,
                DateTimeOffset.UtcNow,
                emptyReport,
                emptySub,
                EtsyOrderCostAccountSplit.Empty,
                Array.Empty<EtsyDashboardReceipt>(),
                Array.Empty<EtsyLedgerEntryDetail>());
        }

        var resolution = await ResolveCostsAsync(shopId, startDate, endDate, entries, cancellationToken);
        var report = EtsyLedgerFinancialEngine.BuildReport(entries, resolution.CostsByDay);
        var subBreakdown = EtsyLedgerFinancialEngine.BuildSubBreakdown(entries);
        var costSplit = BuildCostAccountSplit(resolution.Lines);

        return new EtsyAccountingLedgerData(
            true,
            resolution.CostWarning,
            DateTimeOffset.UtcNow,
            report,
            subBreakdown,
            costSplit,
            resolution.Receipts,
            entries);
    }

    private async Task<(bool Ok, string? Warning, IReadOnlyList<EtsyLedgerEntryDetail> Entries)> FetchEntriesWithRetryAsync(
        string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken)
    {
        try
        {
            var entries = await dataClient.GetLedgerEntriesDetailedAsync(shopId, startDate, endDate, cancellationToken);
            return (true, null, entries);
        }
        catch (Exception firstEx) when (firstEx is not OperationCanceledException)
        {
            _ = firstEx;
            // Defter ucu zaman zaman dalgalanir (bilinen durum): bir kez yeniden denenir.
            await Task.Delay(1500, cancellationToken);
            try
            {
                var entries = await dataClient.GetLedgerEntriesDetailedAsync(shopId, startDate, endDate, cancellationToken);
                return (true, null, entries);
            }
            catch (Exception retryEx) when (retryEx is not OperationCanceledException)
            {
                _ = retryEx;
                return (false, "Canlı Etsy defterine ulaşılamadı; yedek özet gösteriliyor.", Array.Empty<EtsyLedgerEntryDetail>());
            }
        }
    }

    private sealed record ResolvedOrderCostEvent(
        DateTime Day,
        long ReceiptId,
        string BuyerName,
        DateTimeOffset Date,
        bool IsCanceledOrRefunded,
        bool IsCrossPeriodRefund,
        decimal ProductCostUsd,
        decimal ShippingCostUsd,
        decimal PackagingCostUsd,
        decimal TotalCostUsd);

    private sealed record CostResolutionResult(
        IReadOnlyDictionary<DateTime, decimal> CostsByDay,
        int ReceiptCount,
        IReadOnlyList<EtsyDashboardReceipt> Receipts,
        IReadOnlyList<ResolvedOrderCostEvent> Lines,
        string? CostWarning);

    /// <summary>
    /// Siparis/urun maliyetlerini gun bazinda toplar; masaustu orderSummaries.ProductCost semantigi:
    /// yalniz odenmis (veya iptal) siparisler sayilir, iptallerde maliyet girilmisse zarara dahil edilir.
    /// Ayrica donem icinde iade edilen capraz donem siparislerin maliyetleri de iade gunune eklenir.
    /// </summary>
    private async Task<CostResolutionResult> ResolveCostsAsync(
        string shopId, DateTimeOffset startDate, DateTimeOffset endDate, IReadOnlyList<EtsyLedgerEntryDetail> entries, CancellationToken cancellationToken)
    {
        var costsByDay = new Dictionary<DateTime, decimal>();
        var receiptCount = 0;
        var receipts = (IReadOnlyList<EtsyDashboardReceipt>)Array.Empty<EtsyDashboardReceipt>();
        var lines = new List<ResolvedOrderCostEvent>();
        string? costWarning = null;

        try
        {
            receipts = await dataClient.GetShopReceiptsAsync(shopId, startDate, endDate, cancellationToken);
            receiptCount = receipts.Count(r => r.IsPaid || r.IsCanceled);
            var costs = await repository.GetOrderCostsAsync(shopId, cancellationToken);
            var periodReceiptIds = new HashSet<long>(receipts.Select(r => r.ReceiptId));

            // Defterde iade kaydi bulunan donem ici siparisleri isaretle
            var refundedPeriodReceiptIds = new HashSet<long>(
                entries.Where(e => e.Type == "refund" && e.ReferenceId > 0)
                    .Select(e => e.ReferenceId));

            foreach (var receipt in receipts)
            {
                if (!receipt.IsPaid && !receipt.IsCanceled) continue;
                if (!costs.TryGetValue(receipt.ReceiptId, out var cost)) continue;

                var amount = Math.Round(cost.ProductCost + cost.ShippingCost + cost.PackagingCost, 2);
                if (receipt.IsCanceled && amount <= 0) continue;

                var (pUsd, sUsd, pkUsd) = NormalizeCostBreakdown(cost, amount);
                var day = receipt.CreatedAt.UtcDateTime.Date;
                costsByDay[day] = costsByDay.TryGetValue(day, out var existing) ? existing + amount : amount;

                var isCanceledOrRefunded = receipt.IsCanceled || refundedPeriodReceiptIds.Contains(receipt.ReceiptId);
                lines.Add(new ResolvedOrderCostEvent(
                    day,
                    receipt.ReceiptId,
                    receipt.BuyerName ?? "Misafir Müşteri",
                    receipt.CreatedAt,
                    isCanceledOrRefunded,
                    IsCrossPeriodRefund: false,
                    pUsd,
                    sUsd,
                    pkUsd,
                    amount));
            }

            if (costs.Count > 0)
            {
                await AddCrossPeriodRefundCostsAsync(shopId, startDate, endDate, entries, costs, periodReceiptIds, costsByDay, lines, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Siparis/maliyet tarafi alinamazsa rapor maliyetsiz uretilir; defter tarafi yine canlidir.
            costWarning = $"Sipariş veya maliyet bilgilerinin bir kısmı alınamadı: {ex.Message}";
        }

        return new CostResolutionResult(costsByDay, receiptCount, receipts, lines, costWarning);
    }

    private static (decimal ProductUsd, decimal ShippingUsd, decimal PackagingUsd) NormalizeCostBreakdown(
        EtsyDashboardOrderCost cost, decimal roundedTotalUsd)
    {
        var pUsd = Math.Round(cost.ProductCost, 2);
        var sUsd = Math.Round(cost.ShippingCost, 2);
        var pkUsd = Math.Round(cost.PackagingCost, 2);
        var diff = roundedTotalUsd - (pUsd + sUsd + pkUsd);
        if (diff != 0m)
        {
            if (pkUsd > 0m) pkUsd += diff;
            else if (sUsd > 0m) sUsd += diff;
            else pUsd += diff;
        }
        return (pUsd, sUsd, pkUsd);
    }

    private async Task AddCrossPeriodRefundCostsAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        IReadOnlyList<EtsyLedgerEntryDetail> entries,
        IReadOnlyDictionary<long, EtsyDashboardOrderCost> costs,
        HashSet<long> periodReceiptIds,
        Dictionary<DateTime, decimal> costsByDay,
        List<ResolvedOrderCostEvent> lines,
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
                IReadOnlyList<EtsyDashboardReceipt>? fallbackReceipts = null;
                try
                {
                    fallbackReceipts = await dataClient.GetReceiptsModifiedBetweenAsync(shopId, startDate, endDate, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fallbackReceipts = null;
                }
                if (fallbackReceipts is null || fallbackReceipts.Count == 0)
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

        var resolvedPeriodRefundIds = new HashSet<long>();
        var countedCrossPeriodIds = new HashSet<long>();
        for (var i = 0; i < refundEntries.Count; i++)
        {
            if (!resolvedByEntry.TryGetValue(i, out var rid)) continue;
            if (periodReceiptIds.Contains(rid))
            {
                resolvedPeriodRefundIds.Add(rid);
                continue;
            }
            if (!countedCrossPeriodIds.Add(rid)) continue;
            if (!costs.TryGetValue(rid, out var cost)) continue;

            var amount = Math.Round(cost.ProductCost + cost.ShippingCost + cost.PackagingCost, 2);
            if (amount <= 0) continue;

            var (pUsd, sUsd, pkUsd) = NormalizeCostBreakdown(cost, amount);
            var refundEntry = refundEntries[i];
            var day = refundEntry.CreatedAt.UtcDateTime.Date;
            costsByDay[day] = costsByDay.TryGetValue(day, out var existing) ? existing + amount : amount;

            lines.Add(new ResolvedOrderCostEvent(
                day,
                rid,
                $"İade #{rid}",
                refundEntry.CreatedAt,
                IsCanceledOrRefunded: true,
                IsCrossPeriodRefund: true,
                pUsd,
                sUsd,
                pkUsd,
                amount));
        }

        if (resolvedPeriodRefundIds.Count > 0)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (!lines[i].IsCanceledOrRefunded && resolvedPeriodRefundIds.Contains(lines[i].ReceiptId))
                {
                    lines[i] = lines[i] with { IsCanceledOrRefunded = true };
                }
            }
        }
    }

    /// <summary>
    /// Günlük <see cref="EtsyLedgerFinancialEngine.BuildReport"/> TRY yuvarlaması (Math.Round(dayTotalUsd * dayRate, 2))
    /// ile kuruşu kuruşuna eşleşen aktif vs iptal/iade (Ürün / Kargo / Paketleme) dağıtımı üretir.
    /// </summary>
    private static EtsyOrderCostAccountSplit BuildCostAccountSplit(IReadOnlyList<ResolvedOrderCostEvent> events)
    {
        if (events.Count == 0) return EtsyOrderCostAccountSplit.Empty;

        decimal activeProdUsd = 0m, activeShipUsd = 0m, activePackUsd = 0m;
        decimal activeProdTry = 0m, activeShipTry = 0m, activePackTry = 0m;
        decimal canceledProdUsd = 0m, canceledShipUsd = 0m, canceledPackUsd = 0m;
        decimal canceledProdTry = 0m, canceledShipTry = 0m, canceledPackTry = 0m;
        int activeCount = 0, canceledCount = 0;
        var outLines = new List<EtsyOrderCostAccountingLine>(events.Count);

        foreach (var dayGroup in events.GroupBy(e => e.Day).OrderBy(g => g.Key))
        {
            var dayRate = HistoricalExchangeRateProvider.GetRateForDate(dayGroup.Key, DefaultFallbackExchangeRate);
            var dayList = dayGroup.ToList();
            var dayTotalUsd = dayList.Sum(x => x.TotalCostUsd);
            var dayTotalTry = Math.Round(dayTotalUsd * dayRate, 2);

            decimal dayApU = 0m, dayAsU = 0m, dayApkU = 0m;
            decimal dayCpU = 0m, dayCsU = 0m, dayCpkU = 0m;

            foreach (var ev in dayList)
            {
                if (ev.IsCanceledOrRefunded)
                {
                    canceledCount++;
                    dayCpU += ev.ProductCostUsd;
                    dayCsU += ev.ShippingCostUsd;
                    dayCpkU += ev.PackagingCostUsd;
                }
                else
                {
                    activeCount++;
                    dayApU += ev.ProductCostUsd;
                    dayAsU += ev.ShippingCostUsd;
                    dayApkU += ev.PackagingCostUsd;
                }

                var lineTry = Math.Round(ev.TotalCostUsd * dayRate, 2);
                outLines.Add(new EtsyOrderCostAccountingLine(
                    ev.ReceiptId,
                    ev.BuyerName,
                    ev.Date,
                    ev.IsCanceledOrRefunded,
                    ev.IsCrossPeriodRefund,
                    ev.ProductCostUsd,
                    ev.ShippingCostUsd,
                    ev.PackagingCostUsd,
                    ev.TotalCostUsd,
                    lineTry,
                    dayRate));
            }

            activeProdUsd += dayApU;
            activeShipUsd += dayAsU;
            activePackUsd += dayApkU;
            canceledProdUsd += dayCpU;
            canceledShipUsd += dayCsU;
            canceledPackUsd += dayCpkU;

            var apT = Math.Round(dayApU * dayRate, 2);
            var asT = Math.Round(dayAsU * dayRate, 2);
            var apkT = Math.Round(dayApkU * dayRate, 2);
            var cpT = Math.Round(dayCpU * dayRate, 2);
            var csT = Math.Round(dayCsU * dayRate, 2);
            var cpkT = Math.Round(dayCpkU * dayRate, 2);

            // Gün içi yuvarlama farkını BuildReport'un günlük toplamına (dayTotalTry) tam oturt
            var remainder = dayTotalTry - (apT + asT + apkT + cpT + csT + cpkT);
            if (remainder != 0m)
            {
                if (dayCpU >= dayApU && dayCpU > 0m) cpT += remainder;
                else if (dayApU > 0m) apT += remainder;
                else if (dayAsU > 0m) asT += remainder;
                else if (dayCsU > 0m) csT += remainder;
                else if (dayApkU > 0m) apkT += remainder;
                else if (dayCpkU > 0m) cpkT += remainder;
            }

            activeProdTry += apT;
            activeShipTry += asT;
            activePackTry += apkT;
            canceledProdTry += cpT;
            canceledShipTry += csT;
            canceledPackTry += cpkT;
        }

        return new EtsyOrderCostAccountSplit(
            activeProdUsd, activeProdTry,
            activeShipUsd, activeShipTry,
            activePackUsd, activePackTry,
            canceledProdUsd, canceledProdTry,
            canceledShipUsd, canceledShipTry,
            canceledPackUsd, canceledPackTry,
            activeCount, canceledCount,
            outLines);
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
