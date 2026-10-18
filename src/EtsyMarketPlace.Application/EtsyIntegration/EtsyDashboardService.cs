namespace EtsyMarketPlace.Application.EtsyIntegration;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Domain.Banking;

public sealed class EtsyDashboardService : IEtsyDashboardService
{
    private readonly IEtsyReportingService _reporting;
    private readonly IEtsyLedgerReportService _ledgerReport;
    private readonly IEtsyDataClient _etsyClient;
    private readonly IEtsyIntegrationRepository _repository;
    private readonly IBankDepositService _bankService;

    public EtsyDashboardService(
        IEtsyReportingService reporting,
        IEtsyLedgerReportService ledgerReport,
        IEtsyDataClient etsyClient,
        IEtsyIntegrationRepository repository,
        IBankDepositService bankService)
    {
        _reporting = reporting ?? throw new ArgumentNullException(nameof(reporting));
        _ledgerReport = ledgerReport ?? throw new ArgumentNullException(nameof(ledgerReport));
        _etsyClient = etsyClient ?? throw new ArgumentNullException(nameof(etsyClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
    }

    private static async Task<(IReadOnlyList<EtsyDashboardLedgerFee> Entries, string? Error)> SafeGetLedgerAsync(
        IEtsyDataClient client,
        string shopId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var entries = await client.GetPaymentAccountLedgerEntriesAsync(shopId, start, end, ct);
                return (entries, null);
            }
            catch (Exception ex)
            {
                if (attempt == 2)
                {
                    return (Array.Empty<EtsyDashboardLedgerFee>(), ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        return (Array.Empty<EtsyDashboardLedgerFee>(), "bilinmeyen hata");
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ResolvePeriod(string? period, DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        var now = DateTimeOffset.UtcNow;
        if (startDate.HasValue && endDate.HasValue)
        {
            if (startDate.Value > endDate.Value)
                throw new ArgumentException("startDate endDate değerinden sonra olamaz.");
            return (startDate.Value, endDate.Value);
        }

        return period?.ToLowerInvariant() switch
        {
            "today" => (now.Date, now),
            "last_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)),
            null or "" or "this_month" => (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero), now),
            _ => throw new ArgumentException("period today, this_month veya last_month olmalıdır.")
        };
    }

    public async Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var payouts = await _reporting.GetBankPayoutsAsync(shopId, startDate, endDate, cancellationToken);
        if (payouts.Count > 0) return payouts;

        try
        {
            var livePayouts = await _etsyClient.GetBankPayoutsAsync(shopId, startDate, endDate, cancellationToken);
            if (livePayouts.Count > 0)
            {
                await _repository.SavePayoutsAsync(livePayouts, cancellationToken);
                return livePayouts;
            }
        }
        catch { }

        return payouts;
    }

    public async Task<LiveFinancialPerformanceResult> GetFinancialPerformanceAsync(
        string shopId,
        string period,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolvePeriod(period, startDate, endDate);
        var payouts = await GetBankPayoutsAsync(shopId, start, end, cancellationToken);
        var live = await _ledgerReport.GetLiveReportAsync(shopId, start, end, cancellationToken);
        if (live.LedgerOk)
        {
            var p = live.Performance;
            var rateForPayout = p.ExchangeRateUsed ?? 49.30m;
            var totalPayoutsUsd = payouts.Sum(x => Math.Abs(x.Amount));
            var totalPayoutsTry = payouts.Sum(x => Math.Abs(x.Amount) * (x.ExchangeRateToTry ?? rateForPayout));

            return new LiveFinancialPerformanceResult(
                GrossSales: p.GrossSales,
                PlatformFees: p.PlatformFees,
                InternalAdsCost: p.InternalAdsCost,
                ExternalAdsCost: p.ExternalAdsCost,
                ProductCosts: p.ProductCosts,
                ShippingCosts: p.ShippingCosts,
                Refunds: p.Refunds,
                NetProfit: p.NetProfit,
                NetProfitMargin: p.NetProfitMargin,
                GrossSalesTRY: p.GrossSalesTRY,
                NetProfitTRY: p.NetProfitTRY,
                OrderGrossSalesUSD: null,
                OrderGrossSalesTRY: null,
                OrderNetProfitTRY: null,
                ExchangeRateUsed: p.ExchangeRateUsed,
                ReceiptCount: live.ReceiptCount,
                LedgerOk: true,
                LedgerWarning: live.Warning,
                Source: live.Source,
                GeneratedAt: live.GeneratedAt,
                BankPayoutsUSD: totalPayoutsUsd,
                BankPayoutsTRY: Math.Round(totalPayoutsTry, 2));
        }

        var legacy = await _reporting.GetFinancialPerformanceAsync(shopId, start, end, cancellationToken);
        var legRate = legacy.ExchangeRateUsed ?? 48.25m;
        var legPayoutsUsd = payouts.Sum(x => Math.Abs(x.Amount));
        var legPayoutsTry = payouts.Sum(x => Math.Abs(x.Amount) * (x.ExchangeRateToTry ?? legRate));

        return new LiveFinancialPerformanceResult(
            GrossSales: legacy.GrossSales,
            PlatformFees: legacy.PlatformFees,
            InternalAdsCost: legacy.InternalAdsCost,
            ExternalAdsCost: legacy.ExternalAdsCost,
            ProductCosts: legacy.ProductCosts,
            ShippingCosts: legacy.ShippingCosts,
            Refunds: legacy.Refunds,
            NetProfit: legacy.NetProfit,
            NetProfitMargin: legacy.NetProfitMargin,
            GrossSalesTRY: legacy.GrossSalesTRY,
            NetProfitTRY: legacy.NetProfitTRY,
            OrderGrossSalesUSD: legacy.OrderGrossSalesUSD,
            OrderGrossSalesTRY: legacy.OrderGrossSalesTRY,
            OrderNetProfitTRY: legacy.OrderNetProfitTRY,
            ExchangeRateUsed: legacy.ExchangeRateUsed,
            ReceiptCount: live.ReceiptCount,
            LedgerOk: false,
            LedgerWarning: live.Warning,
            Source: "legacy-fallback",
            GeneratedAt: live.GeneratedAt,
            BankPayoutsUSD: legPayoutsUsd,
            BankPayoutsTRY: Math.Round(legPayoutsTry, 2));
    }

    public async Task<DailyShopBrief> GetDailyShopBriefAsync(
        string shopId,
        DateTimeOffset? date,
        CancellationToken cancellationToken = default)
    {
        var requestedDate = date ?? DateTimeOffset.UtcNow;
        var briefStart = new DateTimeOffset(requestedDate.Year, requestedDate.Month, requestedDate.Day, 0, 0, 0, requestedDate.Offset);
        var briefEnd = briefStart.AddDays(1).AddTicks(-1);

        var live = await _ledgerReport.GetLiveReportAsync(shopId, briefStart, briefEnd, cancellationToken);
        var performance = live.LedgerOk
            ? live.Performance
            : await _reporting.GetFinancialPerformanceAsync(shopId, briefStart, briefEnd, cancellationToken);

        var payouts = await _reporting.GetBankPayoutsAsync(shopId, briefStart, briefEnd, cancellationToken);
        var alerts = await _reporting.GetUnfulfilledCostAlertsAsync(shopId, cancellationToken);
        var score = performance.GrossSales == 0 ? 0 : Math.Clamp((int)Math.Round(performance.NetProfitMargin), 0, 100);

        return new DailyShopBrief(
            requestedDate,
            score,
            score >= 80 ? "Mükemmel" : score >= 50 ? "Dikkat" : "Riskli",
            0,
            performance.GrossSales,
            performance.NetProfit,
            payouts.Sum(x => x.Amount),
            alerts.Count,
            performance.GrossSalesTRY,
            performance.NetProfitTRY,
            performance.ExchangeRateUsed);
    }

    public async Task<RecentOrdersDashboardResult> GetRecentOrdersAsync(
        string shopId,
        int days,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var clampedDays = Math.Clamp(days, 1, 90);
        var clampedLimit = Math.Clamp(limit, 1, 50);

        var end = DateTimeOffset.UtcNow;
        var start = end.AddDays(-clampedDays);
        var receipts = await _etsyClient.GetShopReceiptsAsync(shopId, start, end, cancellationToken);
        var (ledgerEntries, ledgerError) = await SafeGetLedgerAsync(_etsyClient, shopId, start, end, cancellationToken);
        var costs = await _repository.GetOrderCostsAsync(shopId, cancellationToken);
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, ledgerEntries, costs);

        var dtos = rows.Take(clampedLimit).Select(r => new RecentOrderRowDto(
            Date: r.CreatedAt.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yy", CultureInfo.InvariantCulture),
            ReceiptId: r.ReceiptId.ToString(CultureInfo.InvariantCulture),
            Title: EtsyLiveDashboardCalculator.ShortenTitle(r.Title),
            Quantity: r.Quantity,
            TotalUsd: r.GrandTotal,
            NetProfitUsd: r.NetProfitUsd,
            HasCost: r.HasCostData)).ToList();

        return new RecentOrdersDashboardResult(
            Orders: dtos,
            Count: rows.Count,
            LedgerOk: ledgerError is null,
            LedgerWarning: ledgerError);
    }

    public async Task<OrderFinancialsDashboardResult> GetOrderFinancialsAsync(
        string shopId,
        string period,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = ResolvePeriod(period, startDate, endDate);
        var now = DateTimeOffset.UtcNow;

        var receipts = await _etsyClient.GetShopReceiptsAsync(shopId, start, end, cancellationToken);
        var (ledgerEntries, ledgerError) = await SafeGetLedgerAsync(_etsyClient, shopId, start, end, cancellationToken);
        var costs = await _repository.GetOrderCostsAsync(shopId, cancellationToken);

        // Siparişlerin kendi oluşturulma tarihlerine ait kilitli kurları dinamik hidrate et
        if (receipts.Count > 0)
        {
            var distinctDates = receipts.Select(r => r.CreatedAt.UtcDateTime.Date).Distinct().ToList();
            await HistoricalExchangeRateProvider.HydrateRatesForDatesAsync(distinctDates, cancellationToken);
        }

        var rate = 49.16m;
        try
        {
            var p = await _repository.GetBankPayoutsAsync(shopId, now.AddDays(-30), now, cancellationToken);
            var latestRate = p.FirstOrDefault(x => x.ExchangeRateToTry.HasValue && x.ExchangeRateToTry.Value > 30)?.ExchangeRateToTry;
            if (latestRate.HasValue) rate = latestRate.Value;
        }
        catch { }

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(receipts, ledgerEntries, costs, rate);

        return new OrderFinancialsDashboardResult(
            Orders: rows,
            Count: rows.Count,
            TotalOrderProfitUsd: rows.Sum(r => r.NetProfitUsd),
            TotalOrderProfitTry: rows.Sum(r => r.NetProfitTry),
            ExchangeRateUsed: rate,
            LedgerOk: ledgerError is null,
            LedgerWarning: ledgerError);
    }

    public async Task<DailySeriesDashboardResult> GetDailySeriesAsync(
        string shopId,
        string? month,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = nowUtc.ToOffset(TimeSpan.FromHours(3));
        var targetYear = nowLocal.Year;
        var targetMonth = nowLocal.Month;

        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var parsedYear) && int.TryParse(parts[1], out var parsedMonth) && parsedMonth is >= 1 and <= 12)
            {
                targetYear = parsedYear;
                targetMonth = parsedMonth;
            }
            else
            {
                throw new ArgumentException("month parametresi YYYY-MM biciminde olmalidir.");
            }
        }

        var monthKey = $"{targetYear:D4}-{targetMonth:D2}";
        var monthStart = new DateTimeOffset(targetYear, targetMonth, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var monthEnd = monthStart.AddMonths(1);
        var fetchEnd = monthEnd < nowUtc ? monthEnd : nowUtc;

        if (fetchEnd <= monthStart)
        {
            return new DailySeriesDashboardResult(
                Month: monthKey,
                Labels: Array.Empty<string>(),
                GrossSales: Array.Empty<decimal>(),
                NetProfit: Array.Empty<decimal>(),
                TopProductTitle: null,
                TopProductRevenueUsd: 0m,
                OrderCount: 0,
                LedgerOk: true,
                LedgerWarning: null,
                Source: "empty");
        }

        var receipts = await _etsyClient.GetShopReceiptsAsync(shopId, monthStart, fetchEnd, cancellationToken);
        var (ledgerEntries, ledgerError) = await SafeGetLedgerAsync(_etsyClient, shopId, monthStart, fetchEnd, cancellationToken);
        var costs = await _repository.GetOrderCostsAsync(shopId, cancellationToken);
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, ledgerEntries, costs);
        var series = EtsyLiveDashboardCalculator.BuildDailySeries(rows, targetYear, targetMonth, nowUtc);
        var topProduct = EtsyLiveDashboardCalculator.PickTopProduct(rows);

        var grossSeries = series.GrossSales;
        var netSeries = series.NetProfit;
        var seriesSource = "orders-fallback";

        var utcMonthStart = new DateTimeOffset(targetYear, targetMonth, 1, 0, 0, 0, TimeSpan.Zero);
        var utcMonthEnd = utcMonthStart.AddMonths(1);
        var utcFetchEnd = utcMonthEnd < nowUtc ? utcMonthEnd : nowUtc;
        var live = await _ledgerReport.GetLiveReportAsync(shopId, utcMonthStart, utcFetchEnd, cancellationToken);

        if (live.LedgerOk)
        {
            grossSeries = new decimal[series.Labels.Length];
            netSeries = new decimal[series.Labels.Length];
            foreach (var day in live.Daily)
            {
                if (day.Date.Year == targetYear && day.Date.Month == targetMonth && day.Date.Day >= 1 && day.Date.Day <= series.Labels.Length)
                {
                    grossSeries[day.Date.Day - 1] = day.GrossSales;
                    netSeries[day.Date.Day - 1] = day.RealNetProfitUsd;
                }
            }
            seriesSource = live.Source;
        }

        return new DailySeriesDashboardResult(
            Month: monthKey,
            Labels: series.Labels,
            GrossSales: grossSeries,
            NetProfit: netSeries,
            TopProductTitle: topProduct.Title,
            TopProductRevenueUsd: topProduct.Revenue,
            OrderCount: rows.Count,
            LedgerOk: live.LedgerOk,
            LedgerWarning: live.LedgerOk ? null : (live.Warning ?? ledgerError),
            Source: seriesSource);
    }

    public async Task<FinancialSummaryDashboardResult> GetRealFinancialSummaryAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var perf = await GetFinancialPerformanceAsync(shopId, "this_month", null, null, cancellationToken);
        var payouts = await _reporting.GetBankPayoutsAsync(shopId, monthStart, now, cancellationToken);

        var periodName = now.ToString("MMMM yyyy", new CultureInfo("tr-TR"));
        var netRevenue = perf.GrossSales - perf.PlatformFees;

        return new FinancialSummaryDashboardResult(
            Period: periodName,
            GrossSales: perf.GrossSales,
            EtsyFees: perf.PlatformFees,
            NetRevenue: netRevenue,
            ProductCosts: perf.ProductCosts,
            RealNetProfit: perf.NetProfit,
            BankPayoutsTotal: payouts.Sum(p => p.Amount));
    }

    public async Task<MonthlyDepositSummary> GetRealBankDepositsAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-30);
        var payouts = await _reporting.GetBankPayoutsAsync(shopId, start, now, cancellationToken);

        var depositEntries = payouts.Select((p, idx) => new RawDepositEntryInput(
            EntryId: idx + 1,
            ReferenceId: long.TryParse(p.ReferenceId, out var rid) ? rid : idx + 1,
            Type: "deposit",
            Amount: p.Amount,
            NetAmount: p.Amount,
            Currency: p.Currency,
            Description: $"Etsy Payment Account Payout ({p.OccurredAt:yyyy-MM-dd})",
            CreatedAt: p.OccurredAt,
            PreResolvedRate: p.ExchangeRateToTry ?? 49.00m
        )).ToList();

        var latestRate = payouts.FirstOrDefault(p => p.ExchangeRateToTry.HasValue && p.ExchangeRateToTry.Value > 30)?.ExchangeRateToTry ?? 49.00m;
        return _bankService.CalculateMonthlyDeposits(depositEntries, start, now, _ => latestRate, latestRate);
    }
}
