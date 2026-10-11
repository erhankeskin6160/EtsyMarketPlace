namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Domain.Banking;
using Xunit;

public sealed class EtsyDashboardServiceTests
{
    private sealed class FakeReportingService : IEtsyReportingService
    {
        public List<EtsyBankPayout> Payouts { get; set; } = new();

        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Payouts);

        public Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult(new FinancialPerformance(
                StartDate: startDate,
                EndDate: endDate,
                Currency: "USD",
                GrossSales: 1000m,
                PlatformFees: 150m,
                InternalAdsCost: 50m,
                ExternalAdsCost: 20m,
                ProductCosts: 200m,
                ShippingCosts: 80m,
                Refunds: 0m,
                NetProfit: 500m,
                NetProfitMargin: 50m,
                GrossSalesTRY: 49000m,
                NetProfitTRY: 24500m,
                OrderGrossSalesUSD: 1000m,
                OrderGrossSalesTRY: 49000m,
                OrderNetProfitTRY: 24500m,
                ExchangeRateUsed: 49.0m));

        public Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken ct) =>
            Task.FromResult(new DailyShopBrief(date, 90, "Mükemmel", 0, 1000m, 500m, 0m, 0, 49000m, 24500m, 49.0m));

        public Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyOrderCostAlert>>(Array.Empty<EtsyOrderCostAlert>());
    }

    private sealed class FakeLedgerReportService : IEtsyLedgerReportService
    {
        public bool LedgerOk { get; set; } = true;

        public Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
        {
            var perf = new FinancialPerformance(
                StartDate: startDate,
                EndDate: endDate,
                Currency: "USD",
                GrossSales: 2000m,
                PlatformFees: 300m,
                InternalAdsCost: 100m,
                ExternalAdsCost: 40m,
                ProductCosts: 400m,
                ShippingCosts: 160m,
                Refunds: 0m,
                NetProfit: 1000m,
                NetProfitMargin: 50m,
                GrossSalesTRY: 98000m,
                NetProfitTRY: 49000m,
                OrderGrossSalesUSD: null,
                OrderGrossSalesTRY: null,
                OrderNetProfitTRY: null,
                ExchangeRateUsed: 49.0m);
            var daily = new List<EtsyLedgerDailySummary>
            {
                new(
                    Date: new DateTime(2026, 9, 1),
                    GrossSales: 2000m,
                    EtsyFees: 300m,
                    InnerAdFees: 100m,
                    OffsiteAdFees: 40m,
                    Refunds: 0m,
                    Deposits: 0m,
                    ProductCosts: 400m,
                    EtsyNetRevenue: 1700m,
                    RealNetProfitUsd: 1000m,
                    GrossSalesTry: 98000m,
                    EtsyFeesTry: 14700m,
                    InnerAdFeesTry: 4900m,
                    OffsiteAdFeesTry: 1960m,
                    RefundsTry: 0m,
                    ProductCostsTry: 19600m,
                    EtsyNetRevenueTry: 83300m,
                    RealNetProfitTry: 49000m,
                    AverageExchangeRate: 49.0m)
            };
            return Task.FromResult(new EtsyLedgerLiveResult(LedgerOk, null, "live-ledger", DateTimeOffset.UtcNow, perf, daily, 10));
        }
    }

    private sealed class FakeEtsyDataClient : IEtsyDataClient
    {
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());

        public Task<IReadOnlyList<EtsyFinancialTransaction>> GetFinancialTransactionsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyFinancialTransaction>>(Array.Empty<EtsyFinancialTransaction>());

        public Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyOrderCostAlert>>(Array.Empty<EtsyOrderCostAlert>());

        public Task<IReadOnlyList<EtsyDashboardReceipt>> GetShopReceiptsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyDashboardReceipt>>(new List<EtsyDashboardReceipt>
            {
                new(
                    ReceiptId: 12345,
                    CreatedAt: DateTimeOffset.UtcNow,
                    IsPaid: true,
                    IsCanceled: false,
                    IsFromOffsiteAds: false,
                    CurrencyCode: "USD",
                    GrandTotal: 100m,
                    Subtotal: 90m,
                    ShippingCost: 10m,
                    DiscountAmt: 0m,
                    TaxCost: 0m,
                    RefundedAmount: 0m,
                    Items: new List<EtsyDashboardReceiptItem> { new("Handmade Item", 1, 1) },
                    BuyerName: "Handmade Item",
                    BuyerUserId: 1,
                    BuyerEmail: "test@example.com",
                    Status: "completed")
            });

        public Task<IReadOnlyList<EtsyDashboardLedgerFee>> GetPaymentAccountLedgerEntriesAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyDashboardLedgerFee>>(Array.Empty<EtsyDashboardLedgerFee>());

        public Task<IReadOnlyList<EtsyLedgerEntryDetail>> GetLedgerEntriesDetailedAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyLedgerEntryDetail>>(Array.Empty<EtsyLedgerEntryDetail>());
    }

    private sealed class FakeIntegrationRepository : IEtsyIntegrationRepository
    {
        public Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken ct) =>
            Task.FromResult<DateTimeOffset?>(null);

        public Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());

        public Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<EtsyMonthlyOrderSummary>>(Array.Empty<EtsyMonthlyOrderSummary>());
        public Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<EtsyListingTrafficRecord>>(Array.Empty<EtsyListingTrafficRecord>());
        public Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken ct = default) => Task.FromResult<EtsyChartSnapshot?>(null);
        public Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<long, EtsyDashboardOrderCost>>(new Dictionary<long, EtsyDashboardOrderCost>());
        public Task UpsertOrderCostAsync(string shopId, string orderId, decimal? productCost, decimal? shippingCost, decimal? packagingCost, string? notes, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GetFinancialPerformanceAsync_ReturnsLiveLedgerResult()
    {
        var reporting = new FakeReportingService();
        var ledger = new FakeLedgerReportService { LedgerOk = true };
        var etsyClient = new FakeEtsyDataClient();
        var repo = new FakeIntegrationRepository();
        var bank = new BankDepositService();

        var service = new EtsyDashboardService(reporting, ledger, etsyClient, repo, bank);
        var result = await service.GetFinancialPerformanceAsync("53236321", "this_month", null, null);

        Assert.True(result.LedgerOk);
        Assert.Equal(2000m, result.GrossSales);
        Assert.Equal(1000m, result.NetProfit);
        Assert.Equal(50m, result.NetProfitMargin);
    }

    [Fact]
    public async Task GetRealFinancialSummaryAsync_CalculatesLiveValuesWithoutMock()
    {
        var reporting = new FakeReportingService();
        reporting.Payouts.Add(new EtsyBankPayout("53236321", "ref-1", DateTimeOffset.UtcNow, 500m, "USD", 49.0m, "paid", "payout"));
        var ledger = new FakeLedgerReportService { LedgerOk = true };
        var etsyClient = new FakeEtsyDataClient();
        var repo = new FakeIntegrationRepository();
        var bank = new BankDepositService();

        var service = new EtsyDashboardService(reporting, ledger, etsyClient, repo, bank);
        var summary = await service.GetRealFinancialSummaryAsync("53236321");

        Assert.Equal(2000m, summary.GrossSales);
        Assert.Equal(300m, summary.EtsyFees);
        Assert.Equal(1700m, summary.NetRevenue);
        Assert.Equal(400m, summary.ProductCosts);
        Assert.Equal(1000m, summary.RealNetProfit);
        Assert.Equal(500m, summary.BankPayoutsTotal);
    }

    [Fact]
    public async Task GetRecentOrdersAsync_MapsOrderRows()
    {
        var reporting = new FakeReportingService();
        var ledger = new FakeLedgerReportService { LedgerOk = true };
        var etsyClient = new FakeEtsyDataClient();
        var repo = new FakeIntegrationRepository();
        var bank = new BankDepositService();

        var service = new EtsyDashboardService(reporting, ledger, etsyClient, repo, bank);
        var res = await service.GetRecentOrdersAsync("53236321", 30, 10);

        Assert.Single(res.Orders);
        Assert.Equal("12345", res.Orders[0].ReceiptId);
        Assert.Equal(100m, res.Orders[0].TotalUsd);
    }
}
