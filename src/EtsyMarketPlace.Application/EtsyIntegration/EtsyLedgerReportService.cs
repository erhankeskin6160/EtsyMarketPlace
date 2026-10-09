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
    int ReceiptCount);

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

        var (costsByDay, receiptCount) = await BuildProductCostsByDayAsync(shopId, startDate, endDate, cancellationToken);
        var report = EtsyLedgerFinancialEngine.BuildReport(entries, costsByDay);
        var performance = MapToPerformance(startDate, endDate, report);

        return new EtsyLedgerLiveResult(true, null, "etsy-ledger-live", DateTimeOffset.UtcNow, performance, report.Daily, receiptCount);
    }

    /// <summary>
    /// Siparis/urun maliyetlerini gun bazinda toplar; masaustu orderSummaries.ProductCost semantigi:
    /// yalniz odanmis (veya iptal) siparisler sayilir, iptallerin maliyeti sifirlanir.
    /// </summary>
    private async Task<(IReadOnlyDictionary<DateTime, decimal> Costs, int ReceiptCount)> BuildProductCostsByDayAsync(
        string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken)
    {
        var costsByDay = new Dictionary<DateTime, decimal>();
        var receiptCount = 0;

        try
        {
            var receipts = await dataClient.GetShopReceiptsAsync(shopId, startDate, endDate, cancellationToken);
            receiptCount = receipts.Count(r => r.IsPaid || r.IsCanceled);
            var costs = await repository.GetOrderCostsAsync(shopId, cancellationToken);

            foreach (var receipt in receipts)
            {
                if (!receipt.IsPaid && !receipt.IsCanceled) continue;
                if (receipt.IsCanceled) continue;
                if (!costs.TryGetValue(receipt.ReceiptId, out var cost)) continue;

                var day = receipt.CreatedAt.UtcDateTime.Date;
                var amount = Math.Round(cost.ProductCost + cost.ShippingCost, 2);
                costsByDay[day] = costsByDay.TryGetValue(day, out var existing) ? existing + amount : amount;
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
            report.AverageExchangeRate);
    }

    private static FinancialPerformance EmptyPerformance(DateTimeOffset startDate, DateTimeOffset endDate) =>
        new(startDate, endDate, "USD", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
}
