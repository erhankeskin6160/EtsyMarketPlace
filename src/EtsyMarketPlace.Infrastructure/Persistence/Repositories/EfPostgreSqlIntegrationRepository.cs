using EtsyMarketPlace.Domain.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL EF Core tabanlı finansal entegrasyon ve raporlama repository uygulaması (Clean Architecture Persistence).
/// </summary>
public class EfPostgreSqlIntegrationRepository : IEtsyIntegrationRepository, IEtsyReportingService
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlIntegrationRepository> _logger;

    public EfPostgreSqlIntegrationRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlIntegrationRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task EnsureShopExistsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return;
        var exists = await _context.ShopConnections.AnyAsync(s => s.ShopId == shopId, cancellationToken);
        if (!exists)
        {
            _context.ShopConnections.Add(new ShopConnection
            {
                ShopId = shopId,
                ShopName = shopId,
                UserId = "system",
                IsActive = true,
                LastSynchronizedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken cancellationToken = default)
    {
        var syncState = await _context.SyncStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ShopId == shopId && s.DataType == dataType, cancellationToken);

        return syncState?.LastCursorAt.HasValue == true
            ? syncState.LastCursorAt.Value.ToUtcOffset()
            : null;
    }

    public async Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken = default)
    {
        if (payouts == null || payouts.Count == 0) return;

        var shopIds = payouts.Select(p => p.ShopId).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
        foreach (var sid in shopIds)
            await EnsureShopExistsAsync(sid, cancellationToken);

        foreach (var p in payouts)
        {
            var existing = await _context.BankPayouts
                .FirstOrDefaultAsync(x => x.ShopId == p.ShopId && x.ReferenceId == p.ReferenceId, cancellationToken);

            if (existing is null)
            {
                _context.BankPayouts.Add(new BankPayout
                {
                    ShopId = p.ShopId,
                    ReferenceId = p.ReferenceId,
                    OccurredAt = p.OccurredAt.UtcDateTime,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    ExchangeRateToTry = p.ExchangeRateToTry,
                    Status = p.Status,
                    Description = p.Description
                });
            }
            else
            {
                existing.OccurredAt = p.OccurredAt.UtcDateTime;
                existing.Amount = p.Amount;
                existing.Currency = p.Currency;
                existing.ExchangeRateToTry = p.ExchangeRateToTry;
                existing.Status = p.Status;
                existing.Description = p.Description;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var startUtc = startDate.UtcDateTime;
            var endUtc = endDate.UtcDateTime;

            var entities = await _context.BankPayouts
                .AsNoTracking()
                .Where(p => p.ShopId == shopId && p.OccurredAt >= startUtc && p.OccurredAt <= endUtc)
                .OrderByDescending(p => p.OccurredAt)
                .ToListAsync(cancellationToken);

            return entities.Select(e => new EtsyBankPayout(
                e.ShopId,
                e.ReferenceId,
                e.OccurredAt.ToUtcOffset(),
                e.Amount,
                e.Currency,
                e.ExchangeRateToTry,
                e.Status,
                e.Description
            )).ToList();
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException || ex.InnerException is Npgsql.NpgsqlException)
        {
            return [];
        }
    }

    public async Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken cancellationToken = default)
    {
        if (transactions == null || transactions.Count == 0) return;

        var shopIds = transactions.Select(t => t.ShopId).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
        foreach (var sid in shopIds)
            await EnsureShopExistsAsync(sid, cancellationToken);

        foreach (var t in transactions)
        {
            var existing = await _context.FinancialTransactions
                .FirstOrDefaultAsync(x => x.ShopId == t.ShopId && x.ReferenceId == t.ReferenceId, cancellationToken);

            if (existing is null)
            {
                _context.FinancialTransactions.Add(new FinancialTransaction
                {
                    ShopId = t.ShopId,
                    ReferenceId = t.ReferenceId,
                    OccurredAt = t.OccurredAt.UtcDateTime,
                    GrossSales = t.GrossSales,
                    PlatformFees = t.PlatformFees,
                    InternalAdsCost = t.InternalAdsCost,
                    ExternalAdsCost = t.ExternalAdsCost,
                    ProductCost = t.ProductCost,
                    ShippingCost = t.ShippingCost,
                    Refunds = t.Refunds,
                    Currency = t.Currency
                });
            }
            else
            {
                existing.OccurredAt = t.OccurredAt.UtcDateTime;
                existing.GrossSales = t.GrossSales;
                existing.PlatformFees = t.PlatformFees;
                existing.InternalAdsCost = t.InternalAdsCost;
                existing.ExternalAdsCost = t.ExternalAdsCost;
                existing.ProductCost = t.ProductCost;
                existing.ShippingCost = t.ShippingCost;
                existing.Refunds = t.Refunds;
                existing.Currency = t.Currency;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken cancellationToken = default)
    {
        if (alerts == null || alerts.Count == 0) return;

        var shopIds = alerts.Select(a => a.ShopId).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
        foreach (var sid in shopIds)
            await EnsureShopExistsAsync(sid, cancellationToken);

        foreach (var a in alerts)
        {
            var existing = await _context.OrderCosts
                .FirstOrDefaultAsync(x => x.ShopId == a.ShopId && x.OrderId == a.OrderId, cancellationToken);

            if (existing is null)
            {
                _context.OrderCosts.Add(new OrderCost
                {
                    ShopId = a.ShopId,
                    OrderId = a.OrderId,
                    CreatedAt = a.CreatedAt.UtcDateTime,
                    Currency = a.Currency,
                    OrderTotal = a.OrderTotal,
                    ProductCost = a.ProductCost,
                    ShippingCost = a.ShippingCost,
                    AlertReason = a.Reason
                });
            }
            else
            {
                existing.CreatedAt = a.CreatedAt.UtcDateTime;
                existing.Currency = a.Currency;
                existing.OrderTotal = a.OrderTotal;
                if (a.ProductCost.HasValue) existing.ProductCost = a.ProductCost;
                if (a.ShippingCost.HasValue) existing.ShippingCost = a.ShippingCost;
                existing.AlertReason = a.Reason;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken cancellationToken = default)
    {
        await EnsureShopExistsAsync(shopId, cancellationToken);

        var existing = await _context.SyncStates
            .FirstOrDefaultAsync(s => s.ShopId == shopId && s.DataType == dataType, cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            _context.SyncStates.Add(new SyncState
            {
                ShopId = shopId,
                DataType = dataType,
                LastSuccessfulSyncAt = error == null ? now : null,
                LastCursorAt = cursor.UtcDateTime,
                LastError = error
            });
        }
        else
        {
            if (error == null) existing.LastSuccessfulSyncAt = now;
            existing.LastCursorAt = cursor.UtcDateTime;
            existing.LastError = error;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken cancellationToken = default)
    {
        if (summaries == null || summaries.Count == 0) return;

        var shopIds = summaries.Select(s => s.ShopId).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
        foreach (var sid in shopIds)
            await EnsureShopExistsAsync(sid, cancellationToken);

        foreach (var s in summaries)
        {
            var existing = await _context.MonthlyOrderSummaries
                .FirstOrDefaultAsync(m => m.ShopId == s.ShopId && m.YearMonth == s.YearMonth, cancellationToken);

            if (existing is null)
            {
                _context.MonthlyOrderSummaries.Add(new MonthlyOrderSummary
                {
                    ShopId = s.ShopId,
                    YearMonth = s.YearMonth,
                    OrderCount = s.OrderCount,
                    UnitsSold = s.UnitsSold,
                    GrossRevenue = s.GrossRevenue,
                    AvgOrderValue = s.AvgOrderValue,
                    ShippedOrders = s.ShippedOrders,
                    UnfulfilledOrders = s.UnfulfilledOrders,
                    Currency = s.Currency ?? "USD"
                });
            }
            else
            {
                existing.OrderCount = s.OrderCount;
                existing.UnitsSold = s.UnitsSold;
                existing.GrossRevenue = s.GrossRevenue;
                existing.AvgOrderValue = s.AvgOrderValue;
                existing.ShippedOrders = s.ShippedOrders;
                existing.UnfulfilledOrders = s.UnfulfilledOrders;
                existing.Currency = s.Currency ?? existing.Currency;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken cancellationToken = default)
    {
        var entities = await _context.MonthlyOrderSummaries
            .AsNoTracking()
            .Where(m => m.ShopId == shopId)
            .OrderByDescending(m => m.YearMonth)
            .Take(months)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new EtsyMonthlyOrderSummary(
            e.ShopId,
            e.YearMonth,
            e.OrderCount,
            e.UnitsSold,
            e.GrossRevenue,
            e.AvgOrderValue,
            e.ShippedOrders,
            e.UnfulfilledOrders,
            e.Currency
        )).ToList();
    }

    public async Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null || records.Count == 0) return;

        var shopIds = records.Select(r => r.ShopId).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
        foreach (var sid in shopIds)
            await EnsureShopExistsAsync(sid, cancellationToken);

        foreach (var r in records)
        {
            var existing = await _context.ListingTrafficDailies
                .FirstOrDefaultAsync(x => x.ShopId == r.ShopId && x.ListingId == r.ListingId && x.SnapshotDate == r.SnapshotDate, cancellationToken);

            if (existing is null)
            {
                _context.ListingTrafficDailies.Add(new ListingTrafficDaily
                {
                    ShopId = r.ShopId,
                    ListingId = r.ListingId,
                    SnapshotDate = r.SnapshotDate,
                    Title = r.Title,
                    Views = r.Views,
                    Favorites = r.Favorites,
                    ViewsToday = r.ViewsToday,
                    FavoritesToday = r.FavoritesToday,
                    UnitsSoldMonth = r.UnitsSoldMonth,
                    RevenueMonth = r.RevenueMonth,
                    ConversionRate = r.ConversionRate,
                    ImageUrl = r.ImageUrl,
                    ListingUrl = r.ListingUrl
                });
            }
            else
            {
                existing.Title = r.Title;
                existing.Views = r.Views;
                existing.Favorites = r.Favorites;
                existing.ViewsToday = r.ViewsToday;
                existing.FavoritesToday = r.FavoritesToday;
                existing.UnitsSoldMonth = r.UnitsSoldMonth;
                existing.RevenueMonth = r.RevenueMonth;
                existing.ConversionRate = r.ConversionRate;
                existing.ImageUrl = r.ImageUrl;
                existing.ListingUrl = r.ListingUrl;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshotDate))
        {
            var maxDate = await _context.ListingTrafficDailies
                .Where(x => x.ShopId == shopId)
                .Select(x => x.SnapshotDate)
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync(cancellationToken);
            snapshotDate = maxDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        var entities = await _context.ListingTrafficDailies
            .AsNoTracking()
            .Where(x => x.ShopId == shopId && x.SnapshotDate == snapshotDate)
            .OrderByDescending(x => x.ViewsToday)
            .ThenByDescending(x => x.Views)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new EtsyListingTrafficRecord(
            e.ShopId,
            e.ListingId,
            e.SnapshotDate,
            e.Title,
            e.Views,
            e.Favorites,
            e.ViewsToday,
            e.FavoritesToday,
            e.UnitsSoldMonth,
            e.RevenueMonth,
            e.ConversionRate,
            e.ImageUrl,
            e.ListingUrl
        )).ToList();
    }

    public async Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await EnsureShopExistsAsync(snapshot.ShopId, cancellationToken);

        var existing = await _context.ChartSnapshots
            .FirstOrDefaultAsync(c => c.ShopId == snapshot.ShopId && c.ChartType == snapshot.ChartType, cancellationToken);

        if (existing is null)
        {
            _context.ChartSnapshots.Add(new ChartSnapshot
            {
                ShopId = snapshot.ShopId,
                ChartType = snapshot.ChartType,
                PeriodStart = snapshot.PeriodStart.ToString("O"),
                PeriodEnd = snapshot.PeriodEnd.ToString("O"),
                ImagePngBase64 = snapshot.ImagePngBase64,
                UpdatedAt = snapshot.UpdatedAt.UtcDateTime
            });
        }
        else
        {
            existing.PeriodStart = snapshot.PeriodStart.ToString("O");
            existing.PeriodEnd = snapshot.PeriodEnd.ToString("O");
            existing.ImagePngBase64 = snapshot.ImagePngBase64;
            existing.UpdatedAt = snapshot.UpdatedAt.UtcDateTime;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ChartSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.ChartType == chartType, cancellationToken);

        if (entity is null) return null;

        var start = DateTimeOffset.TryParse(entity.PeriodStart, out var s) ? s : DateTimeOffset.UtcNow;
        var end = DateTimeOffset.TryParse(entity.PeriodEnd, out var e) ? e : DateTimeOffset.UtcNow;

        return new EtsyChartSnapshot(
            entity.ShopId,
            entity.ChartType,
            start,
            end,
            entity.ImagePngBase64,
            entity.UpdatedAt.ToUtcOffset()
        );
    }

    public async Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await _context.OrderCosts
                .AsNoTracking()
                .Where(o => o.ShopId == shopId && (o.ProductCost != null || o.ShippingCost != null))
                .ToListAsync(cancellationToken);

            var dict = new Dictionary<long, EtsyDashboardOrderCost>();
            foreach (var e in entities)
            {
                if (long.TryParse(e.OrderId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var receiptId))
                {
                    dict[receiptId] = new EtsyDashboardOrderCost(
                        e.ProductCost ?? 0m,
                        e.ShippingCost ?? 0m
                    );
                }
            }

            return dict;
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException || ex.InnerException is Npgsql.NpgsqlException)
        {
            return new Dictionary<long, EtsyDashboardOrderCost>();
        }
    }

    public async Task UpsertOrderCostAsync(string shopId, string orderId, decimal? productCost, decimal? shippingCost, decimal? packagingCost, string? notes, CancellationToken cancellationToken = default)
    {
        await EnsureShopExistsAsync(shopId, cancellationToken);

        var existing = await _context.OrderCosts
            .FirstOrDefaultAsync(o => o.ShopId == shopId && o.OrderId == orderId, cancellationToken);

        if (existing is null)
        {
            _context.OrderCosts.Add(new OrderCost
            {
                ShopId = shopId,
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow,
                Currency = "USD",
                OrderTotal = 0,
                ProductCost = productCost,
                ShippingCost = shippingCost,
                AlertReason = notes
            });
        }
        else
        {
            if (productCost.HasValue) existing.ProductCost = productCost;
            if (shippingCost.HasValue) existing.ShippingCost = shippingCost;
            if (notes is not null) existing.AlertReason = notes;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var startUtc = startDate.UtcDateTime;
            var endUtc = endDate.UtcDateTime;

            var txs = await _context.FinancialTransactions
                .AsNoTracking()
                .Where(t => t.ShopId == shopId && t.OccurredAt >= startUtc && t.OccurredAt <= endUtc)
                .ToListAsync(cancellationToken);

            decimal gross = txs.Sum(t => t.GrossSales);
            decimal fees = txs.Sum(t => t.PlatformFees);
            decimal internalAds = txs.Sum(t => t.InternalAdsCost);
            decimal externalAds = txs.Sum(t => t.ExternalAdsCost);
            decimal product = txs.Sum(t => t.ProductCost);
            decimal shipping = txs.Sum(t => t.ShippingCost);
            decimal refunds = txs.Sum(t => t.Refunds);
            string currency = txs.FirstOrDefault()?.Currency ?? "USD";

            var net = gross - fees - internalAds - externalAds - product - shipping - refunds;
            var margin = gross == 0 ? 0 : net / gross * 100;

            // 1. En güncel döviz kurunu çek (bank_payouts tablosundaki kur veya varsayılan 48.25)
            var latestPayout = await _context.BankPayouts
                .AsNoTracking()
                .Where(p => p.ShopId == shopId && p.ExchangeRateToTry > 30)
                .OrderByDescending(p => p.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            decimal exchangeRate = latestPayout?.ExchangeRateToTry ?? 48.25m;

            // 2. monthly_order_summaries tablosundan sipariş bazlı ciroyu çek (Masaüstü paneliyle birebir senkron için)
            var startMonth = startDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var endMonth = endDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            var monthlyTurnoverList = await _context.MonthlyOrderSummaries
                .AsNoTracking()
                .Where(m => m.ShopId == shopId && string.Compare(m.YearMonth, startMonth) >= 0 && string.Compare(m.YearMonth, endMonth) <= 0)
                .Select(m => m.GrossRevenue)
                .ToListAsync(cancellationToken);

            decimal? orderGrossUSD = monthlyTurnoverList.Count > 0 ? monthlyTurnoverList.Sum() : null;

            decimal grossSalesTRY = Math.Round(gross * exchangeRate, 2);
            decimal netProfitTRY = Math.Round(net * exchangeRate, 2);

            decimal? orderGrossSalesTRY = orderGrossUSD.HasValue ? Math.Round(orderGrossUSD.Value * exchangeRate, 2) : null;
            decimal? orderNetProfitTRY = orderGrossUSD.HasValue
                ? Math.Round((orderGrossUSD.Value - fees - internalAds - externalAds - product - shipping - refunds) * exchangeRate, 2)
                : null;

            return new FinancialPerformance(
                startDate,
                endDate,
                currency,
                gross,
                fees,
                internalAds,
                externalAds,
                product,
                shipping,
                refunds,
                net,
                margin,
                grossSalesTRY,
                netProfitTRY,
                orderGrossUSD,
                orderGrossSalesTRY,
                orderNetProfitTRY,
                exchangeRate
            );
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException || ex.InnerException is Npgsql.NpgsqlException)
        {
            return new FinancialPerformance(
                startDate,
                endDate,
                "USD",
                0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m,
                null, null,
                null, null,
                null,
                48.25m);
        }
    }

    public async Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        try
        {
            var costs = await _context.OrderCosts
                .AsNoTracking()
                .Where(c => c.ShopId == shopId && (!c.ProductCost.HasValue || !c.ShippingCost.HasValue))
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            return costs.Select(c => new EtsyOrderCostAlert(
                c.ShopId,
                c.OrderId,
                c.CreatedAt.ToUtcOffset(),
                c.Currency,
                c.OrderTotal,
                c.ProductCost,
                c.ShippingCost,
                c.AlertReason ?? "Maliyet bilgisi eksik."
            )).ToList();
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException || ex.InnerException is Npgsql.NpgsqlException)
        {
            return [];
        }
    }

    public async Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken cancellationToken = default)
    {
        var start = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);
        var end = start.AddDays(1).AddTicks(-1);

        var performance = await GetFinancialPerformanceAsync(shopId, start, end, cancellationToken);
        var payouts = await GetBankPayoutsAsync(shopId, start, end, cancellationToken);
        var alerts = await GetUnfulfilledCostAlertsAsync(shopId, cancellationToken);

        var score = performance.GrossSales == 0 ? 0 : Math.Clamp((int)Math.Round(performance.NetProfitMargin), 0, 100);
        string status = score >= 80 ? "Mükemmel" : score >= 50 ? "Dikkat" : "Riskli";

        return new DailyShopBrief(
            date,
            score,
            status,
            0,
            performance.GrossSales,
            performance.NetProfit,
            payouts.Sum(x => x.Amount),
            alerts.Count,
            performance.GrossSalesTRY,
            performance.NetProfitTRY,
            performance.ExchangeRateUsed
        );
    }
}
