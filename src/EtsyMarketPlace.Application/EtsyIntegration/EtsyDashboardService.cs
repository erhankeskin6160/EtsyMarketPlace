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
    private readonly EtsyMarketPlace.Domain.Orders.IOrderAddressRepository? _addressRepository;

    public EtsyDashboardService(
        IEtsyReportingService reporting,
        IEtsyLedgerReportService ledgerReport,
        IEtsyDataClient etsyClient,
        IEtsyIntegrationRepository repository,
        IBankDepositService bankService,
        EtsyMarketPlace.Domain.Orders.IOrderAddressRepository? addressRepository = null)
    {
        _reporting = reporting ?? throw new ArgumentNullException(nameof(reporting));
        _ledgerReport = ledgerReport ?? throw new ArgumentNullException(nameof(ledgerReport));
        _etsyClient = etsyClient ?? throw new ArgumentNullException(nameof(etsyClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
        _addressRepository = addressRepository;
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

        var trNow = now.ToOffset(TimeSpan.FromHours(3));

        return period?.Trim().ToLowerInvariant() switch
        {
            "today" or "bugun" => (
                new DateTimeOffset(trNow.Year, trNow.Month, trNow.Day, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime(),
                now),
            "last_month" or "gecen_ay" => (
                new DateTimeOffset(trNow.Year, trNow.Month, 1, 0, 0, 0, TimeSpan.FromHours(3)).AddMonths(-1).ToUniversalTime(), 
                new DateTimeOffset(trNow.Year, trNow.Month, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime()),
            "last_90_days" or "90d" or "son_90_gun" => (
                now.AddDays(-90),
                now),
            "this_year" or "bu_yil" => (
                new DateTimeOffset(trNow.Year, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime(),
                now),
            "last_year" or "gecen_yil" => (
                new DateTimeOffset(trNow.Year - 1, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime(),
                new DateTimeOffset(trNow.Year, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime()),
            "all_time" or "all" or "tum_zamanlar" => (
                new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime(),
                now),
            null or "" or "this_month" or "bu_ay" => (
                // Türkiye saat dilimi (UTC+3) ile ay başlangıcı: 1. gün 00:00 TR = Önceki ay son günü 21:00 UTC
                new DateTimeOffset(trNow.Year, trNow.Month, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime(), 
                now),
            _ => (
                // Beklenmeyen periyot parametrelerinde dashboard'u kilitlemek yerine güvenli fallback
                new DateTimeOffset(trNow.Year, trNow.Month, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime(),
                now)
        };
    }

    public async Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var payouts = await _reporting.GetBankPayoutsAsync(shopId, startDate, endDate, cancellationToken);
        try
        {
            var livePayouts = await _etsyClient.GetBankPayoutsAsync(shopId, startDate, endDate, cancellationToken);
            if (livePayouts.Count > 0)
            {
                await _repository.SavePayoutsAsync(livePayouts, cancellationToken);
                payouts = payouts.Concat(livePayouts).ToList();
            }
        }
        catch
        {
            // Canlı API'ye ulaşılamazsa yerel veritabanı kayıtları kullanılır
        }

        if (payouts.Count > 0)
        {
            var distinctDates = payouts.Select(p => p.OccurredAt.UtcDateTime.Date).Distinct().ToList();
            await HistoricalExchangeRateProvider.HydrateRatesForDatesAsync(distinctDates, cancellationToken);
        }

        // Payout filtrelemesi: 'fee' içeren satırlar elenir, mükerrer transferler deduplicate edilir ve tarihsel kur bağlanır
        var validPayouts = payouts
            .Where(p => !p.Description.Contains("fee", StringComparison.OrdinalIgnoreCase) && 
                        !p.Status.Contains("fee", StringComparison.OrdinalIgnoreCase))
            .Select(p =>
            {
                var historicalRate = HistoricalExchangeRateProvider.GetRateForDate(p.OccurredAt.DateTime);
                return p with { ExchangeRateToTry = historicalRate };
            });

        return DeduplicatePayouts(validPayouts);
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
            var totalPayoutsUsd = payouts.Sum(x => Math.Abs(x.Amount));
            var totalPayoutsTry = payouts.Sum(x => Math.Abs(x.Amount) * (x.ExchangeRateToTry ?? HistoricalExchangeRateProvider.GetRateForDate(x.OccurredAt.DateTime)));

            var payoutsByMonth = payouts
                .GroupBy(x => (x.OccurredAt.UtcDateTime.Year, x.OccurredAt.UtcDateTime.Month))
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Usd: g.Sum(x => Math.Abs(x.Amount)),
                        Try: g.Sum(x => Math.Abs(x.Amount) * (x.ExchangeRateToTry ?? HistoricalExchangeRateProvider.GetRateForDate(x.OccurredAt.DateTime)))
                    ));

            var turkishMonthNames = new[]
            {
                "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
            };

            var baseMonthly = live.Monthly ?? Array.Empty<EtsyLedgerMonthlySummary>();
            var monthlyMap = baseMonthly.ToDictionary(m => (m.Year, m.Month));

            var allKeys = monthlyMap.Keys
                .Union(payoutsByMonth.Keys)
                .OrderByDescending(k => k.Year * 100 + k.Month)
                .ToList();

            var combinedMonthly = new List<EtsyLedgerMonthlySummary>();
            foreach (var key in allKeys)
            {
                var hasPayout = payoutsByMonth.TryGetValue(key, out var po);
                var payoutUsd = hasPayout ? Math.Round(po.Usd, 2) : 0m;
                var payoutTry = hasPayout ? Math.Round(po.Try, 2) : 0m;

                if (monthlyMap.TryGetValue(key, out var m))
                {
                    combinedMonthly.Add(m with
                    {
                        BankPayoutsUsd = payoutUsd,
                        BankPayoutsTry = payoutTry
                    });
                }
                else
                {
                    var y = key.Year;
                    var mo = key.Month;
                    var label = mo >= 1 && mo <= 12 ? $"{turkishMonthNames[mo]} {y}" : $"{y}-{mo:D2}";
                    combinedMonthly.Add(new EtsyLedgerMonthlySummary(
                        Year: y,
                        Month: mo,
                        PeriodLabel: label,
                        OrderCount: 0,
                        GrossSalesUsd: 0m,
                        GrossSalesTry: 0m,
                        EtsyFeesUsd: 0m,
                        EtsyFeesTry: 0m,
                        AdFeesUsd: 0m,
                        AdFeesTry: 0m,
                        InnerAdFeesTry: 0m,
                        OffsiteAdFeesTry: 0m,
                        RefundsUsd: 0m,
                        RefundsTry: 0m,
                        ProductCostsUsd: 0m,
                        ProductCostsTry: 0m,
                        BankPayoutsUsd: payoutUsd,
                        BankPayoutsTry: payoutTry,
                        RealNetProfitUsd: 0m,
                        RealNetProfitTry: 0m,
                        ProfitMarginPct: 0m
                    ));
                }
            }

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
                BankPayoutsTRY: Math.Round(totalPayoutsTry, 2),
                MonthlySummaries: combinedMonthly,
                ProductCostsTRY: p.ProductCostsTRY,
                PlatformFeesTRY: p.PlatformFeesTRY,
                InternalAdsCostTRY: p.InternalAdsCostTRY,
                ExternalAdsCostTRY: p.ExternalAdsCostTRY,
                RefundsTRY: p.RefundsTRY);
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
            foreach (var r in receipts)
            {
                foreach (var item in r.Items)
                {
                    if (item.TransactionId > 0 && r.ReceiptId > 0)
                    {
                        EtsyTransactionOrderCache.Register(item.TransactionId, r.ReceiptId, r.BuyerName ?? string.Empty, r.CreatedAt, item.Title ?? string.Empty);
                    }
                }
            }

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

        var buyerNameMap = new Dictionary<long, string>();
        var orderDateMap = new Dictionary<long, DateTimeOffset>();
        var titleMap = new Dictionary<long, string>();
        if (_addressRepository != null)
        {
            try
            {
                var addrDict = await _addressRepository.GetAllAsync(cancellationToken);
                foreach (var kv in addrDict)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value.BuyerName))
                    {
                        buyerNameMap[kv.Key] = kv.Value.BuyerName;
                    }
                    orderDateMap[kv.Key] = new DateTimeOffset(kv.Value.UpdatedAtUtc, TimeSpan.Zero);
                }
            }
            catch { }
        }

        IReadOnlyDictionary<long, long>? ledgerEntryToReceiptMap = null;
        var refundEntryIds = ledgerEntries
            .Where(e => e.EntryId > 0 && string.Equals(e.Type, "refund", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.EntryId)
            .Distinct()
            .ToList();

        if (refundEntryIds.Count > 0)
        {
            try
            {
                ledgerEntryToReceiptMap = await _etsyClient.GetReceiptIdsByLedgerEntriesAsync(shopId, refundEntryIds, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Defter -> siparis esleme ucu gecici olarak erisilemezse mevcut donem akisi korunur
            }
        }

        var periodReceiptIds = new HashSet<long>(receipts.Select(r => r.ReceiptId));
        var crossPeriodReceiptIds = new HashSet<long>();
        if (ledgerEntryToReceiptMap != null)
        {
            foreach (var rid in ledgerEntryToReceiptMap.Values)
            {
                if (rid > 0 && !periodReceiptIds.Contains(rid))
                {
                    crossPeriodReceiptIds.Add(rid);
                }
            }
        }
        var hasUnresolvedRefund = false;
        foreach (var entry in ledgerEntries.Where(e => string.Equals(e.Type, "refund", StringComparison.OrdinalIgnoreCase)))
        {
            var resolved = false;
            if (entry.EntryId > 0 && ledgerEntryToReceiptMap != null && ledgerEntryToReceiptMap.TryGetValue(entry.EntryId, out var mappedRid) && mappedRid > 0)
            {
                resolved = true;
            }
            if (entry.ReferenceId > 0 && EtsyTransactionOrderCache.TryGetOrderReference(entry.ReferenceId, out var cached) && cached != null && cached.ReceiptId > 0)
            {
                resolved = true;
                if (!periodReceiptIds.Contains(cached.ReceiptId))
                {
                    crossPeriodReceiptIds.Add(cached.ReceiptId);
                }
            }
            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                var match = System.Text.RegularExpressions.Regex.Match(entry.Description, @"(?<!\d)(\d{9,11})(?!\d)");
                if (match.Success && long.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None, CultureInfo.InvariantCulture, out var descRid) && descRid > 0)
                {
                    resolved = true;
                    if (!periodReceiptIds.Contains(descRid))
                    {
                        crossPeriodReceiptIds.Add(descRid);
                    }
                }
            }
            if (!resolved && entry.ReferenceId > 0 && periodReceiptIds.Contains(entry.ReferenceId))
            {
                resolved = true;
            }
            if (!resolved && Math.Abs(entry.Amount) > 0)
            {
                hasUnresolvedRefund = true;
            }
        }

        IReadOnlyList<EtsyDashboardReceipt>? fallbackReceipts = null;
        if (hasUnresolvedRefund)
        {
            try
            {
                try
                {
                    fallbackReceipts = await _etsyClient.GetReceiptsModifiedBetweenAsync(shopId, start, end, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    fallbackReceipts = null;
                }
                if (fallbackReceipts is null || fallbackReceipts.Count == 0)
                {
                    fallbackReceipts = await _etsyClient.GetShopReceiptsAsync(shopId, start.AddDays(-120), start, cancellationToken);
                }
                foreach (var extReceipt in fallbackReceipts)
                {
                    if (extReceipt.ReceiptId <= 0) continue;
                    if (!string.IsNullOrWhiteSpace(extReceipt.BuyerName) && !buyerNameMap.ContainsKey(extReceipt.ReceiptId))
                    {
                        buyerNameMap[extReceipt.ReceiptId] = extReceipt.BuyerName;
                    }
                    if (extReceipt.CreatedAt != default && !orderDateMap.ContainsKey(extReceipt.ReceiptId))
                    {
                        orderDateMap[extReceipt.ReceiptId] = extReceipt.CreatedAt;
                    }
                    var firstTitle = extReceipt.Items.FirstOrDefault()?.Title;
                    if (!string.IsNullOrWhiteSpace(firstTitle) && !titleMap.ContainsKey(extReceipt.ReceiptId))
                    {
                        titleMap[extReceipt.ReceiptId] = firstTitle;
                    }
                    foreach (var item in extReceipt.Items)
                    {
                        if (item.TransactionId > 0)
                        {
                            EtsyTransactionOrderCache.Register(item.TransactionId, extReceipt.ReceiptId, extReceipt.BuyerName ?? string.Empty, extReceipt.CreatedAt, item.Title ?? string.Empty);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Yedek gecmis fis taramasi basarisiz olursa mevcut akis korunur
            }
        }

        if (crossPeriodReceiptIds.Count > 0)
        {
            var fetched = await Task.WhenAll(crossPeriodReceiptIds.Take(25).Select(async rid =>
            {
                try
                {
                    return await _etsyClient.GetReceiptByIdAsync(shopId, rid, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return null;
                }
            }));

            foreach (var extReceipt in fetched)
            {
                if (extReceipt is null || extReceipt.ReceiptId <= 0) continue;
                if (!string.IsNullOrWhiteSpace(extReceipt.BuyerName))
                {
                    buyerNameMap[extReceipt.ReceiptId] = extReceipt.BuyerName;
                }
                if (extReceipt.CreatedAt != default)
                {
                    orderDateMap[extReceipt.ReceiptId] = extReceipt.CreatedAt;
                }
                var firstTitle = extReceipt.Items.FirstOrDefault()?.Title;
                if (!string.IsNullOrWhiteSpace(firstTitle))
                {
                    titleMap[extReceipt.ReceiptId] = firstTitle;
                }
                foreach (var item in extReceipt.Items)
                {
                    if (item.TransactionId > 0)
                    {
                        EtsyTransactionOrderCache.Register(item.TransactionId, extReceipt.ReceiptId, extReceipt.BuyerName ?? string.Empty, extReceipt.CreatedAt, item.Title ?? string.Empty);
                    }
                }
            }
        }

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            receipts,
            ledgerEntries,
            costs,
            rate,
            buyerNameMap,
            orderDateMap,
            ledgerEntryToReceiptMap,
            titleMap,
            fallbackReceipts);

        var activeOrderRows = rows.Where(r => !string.Equals(r.CostNotes, "cross_period_refund", StringComparison.OrdinalIgnoreCase)).ToList();
        return new OrderFinancialsDashboardResult(
            Orders: rows,
            Count: rows.Count,
            TotalOrderProfitUsd: activeOrderRows.Sum(r => r.NetProfitUsd),
            TotalOrderProfitTry: activeOrderRows.Sum(r => r.NetProfitTry),
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

    private static List<EtsyBankPayout> DeduplicatePayouts(IEnumerable<EtsyBankPayout> source)
    {
        var list = source.OrderByDescending(p => p.OccurredAt).ToList();
        var result = new List<EtsyBankPayout>();
        foreach (var payout in list)
        {
            var existingIndex = result.FindIndex(existing =>
            {
                var diffMinutes = Math.Abs((existing.OccurredAt - payout.OccurredAt).TotalMinutes);
                var isSameTimeOrTzShift = diffMinutes < 2.0 || Math.Abs(diffMinutes - 180.0) < 2.0;
                return isSameTimeOrTzShift && Math.Abs(existing.Amount - payout.Amount) < 0.25m;
            });

            if (existingIndex < 0)
            {
                result.Add(payout);
            }
            else
            {
                // Eğer yeni payout 12 haneli kanonik entry_id ise (2 ile başlayan), 11 haneli disburse ref yerine tercih et
                if (payout.ReferenceId.Length >= result[existingIndex].ReferenceId.Length && payout.ReferenceId.StartsWith("2"))
                {
                    result[existingIndex] = payout;
                }
            }
        }
        return result;
    }
}
