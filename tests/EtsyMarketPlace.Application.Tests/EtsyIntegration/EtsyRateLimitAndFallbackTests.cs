using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Domain.Banking;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.EtsyIntegration;

public sealed class EtsyRateLimitAndFallbackTests
{
    [Fact]
    public async Task EtsyAccessTokenHandler_ConcurrentExpiredTokenRequests_RefreshesOnlyOnce()
    {
        var shopId = $"shop-refresh-{Guid.NewGuid():N}";
        var tokenStore = new InMemoryTokenStore(new EtsyOAuthToken(
            "expired-token",
            "refresh-1",
            DateTimeOffset.UtcNow.AddMinutes(-5)));

        var oauthService = new CountingOAuthService();
        var innerHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new EtsyAccessTokenHandler(tokenStore, oauthService)
        {
            InnerHandler = innerHandler
        };

        using var invoker = new HttpMessageInvoker(handler);

        var tasks = new Task<HttpResponseMessage>[6];
        for (var i = 0; i < tasks.Length; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://openapi.etsy.com/v3/test");
            req.Headers.Add("X-Etsy-Shop-Id", shopId);
            tasks[i] = invoker.SendAsync(req, CancellationToken.None);
        }

        var responses = await Task.WhenAll(tasks);

        Assert.Equal(1, oauthService.RefreshCalls);
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        foreach (var r in responses) r.Dispose();
    }

    [Fact]
    public async Task EtsyAccessTokenHandler_LongRetryAfter429_FailsFastWithoutHanging()
    {
        var shopId = $"shop-429-long-{Guid.NewGuid():N}";
        var tokenStore = new InMemoryTokenStore(new EtsyOAuthToken(
            "valid-token",
            "refresh-1",
            DateTimeOffset.UtcNow.AddHours(1)));

        var innerCalls = 0;
        var innerHandler = new StubHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref innerCalls);
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
            return resp;
        });

        var handler = new EtsyAccessTokenHandler(tokenStore, new CountingOAuthService())
        {
            InnerHandler = innerHandler
        };

        using var invoker = new HttpMessageInvoker(handler);
        var sw = Stopwatch.StartNew();

        using var req1 = new HttpRequestMessage(HttpMethod.Get, "https://openapi.etsy.com/v3/test");
        req1.Headers.Add("X-Etsy-Shop-Id", shopId);
        using var res1 = await invoker.SendAsync(req1, CancellationToken.None);

        using var req2 = new HttpRequestMessage(HttpMethod.Get, "https://openapi.etsy.com/v3/test");
        req2.Headers.Add("X-Etsy-Shop-Id", shopId);
        using var res2 = await invoker.SendAsync(req2, CancellationToken.None);

        sw.Stop();

        Assert.Equal(HttpStatusCode.TooManyRequests, res1.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, res2.StatusCode);
        Assert.Equal(1, innerCalls); // Second call short-circuited by per-shop cooldown
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"Expected fast fail, took {sw.Elapsed}");
    }

    [Fact]
    public async Task EtsyAccessTokenHandler_ShortRetryAfter429_RetriesOnceAndSucceeds()
    {
        var shopId = $"shop-429-short-{Guid.NewGuid():N}";
        var tokenStore = new InMemoryTokenStore(new EtsyOAuthToken(
            "valid-token",
            "refresh-1",
            DateTimeOffset.UtcNow.AddHours(1)));

        var innerCalls = 0;
        var innerHandler = new StubHttpMessageHandler(_ =>
        {
            var call = Interlocked.Increment(ref innerCalls);
            if (call == 1)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                resp.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return resp;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new EtsyAccessTokenHandler(tokenStore, new CountingOAuthService())
        {
            InnerHandler = innerHandler
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://openapi.etsy.com/v3/test");
        req.Headers.Add("X-Etsy-Shop-Id", shopId);
        using var res = await invoker.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(2, innerCalls);
    }

    [Fact]
    public async Task EtsyDashboardService_WhenReceiptsThrow429_ReturnsGracefulDegradedAndStaleFallback()
    {
        var shopId = $"shop-fallback-{Guid.NewGuid():N}";
        var dataClient = new FailingOrSeededEtsyDataClient();
        var service = new EtsyDashboardService(
            new StubReportingService(),
            new StubLedgerReportService(),
            dataClient,
            new StubIntegrationRepository(),
            new StubBankDepositService());

        // 1) Cold cache + 429 => should NOT throw, returns LedgerOk=false
        dataClient.ShouldThrow429 = true;
        var coldRecent = await service.GetRecentOrdersAsync(shopId, 7, 15);
        var coldOrders = await service.GetOrderFinancialsAsync(shopId, "this_month", null, null);
        var coldSeries = await service.GetDailySeriesAsync(shopId, null);

        Assert.False(coldRecent.LedgerOk);
        Assert.Contains("429", coldRecent.LedgerWarning);
        Assert.Empty(coldRecent.Orders);

        Assert.False(coldOrders.LedgerOk);
        Assert.Contains("429", coldOrders.LedgerWarning);
        Assert.Empty(coldOrders.Orders);

        Assert.False(coldSeries.LedgerOk);
        Assert.Equal("etsy-unavailable", coldSeries.Source);

        // 2) Live succeeds => populates stale cache
        dataClient.ShouldThrow429 = false;
        var warmRecent = await service.GetRecentOrdersAsync(shopId, 7, 15);
        Assert.True(warmRecent.LedgerOk);
        Assert.Single(warmRecent.Orders);

        // 3) Next call hits 429 => returns stale cached order with warning!
        dataClient.ShouldThrow429 = true;
        var staleRecent = await service.GetRecentOrdersAsync(shopId, 7, 15);
        Assert.False(staleRecent.LedgerOk);
        Assert.Single(staleRecent.Orders);
        Assert.Contains("önbellek", staleRecent.LedgerWarning);
    }

    [Fact]
    public void EtsyLedgerFinancialEngine_UnclassifiedPositiveEntry_ProducesWarning()
    {
        var entries = new[]
        {
            new EtsyLedgerEntryDetail(
                Type: "unknown_credit",
                RawType: "unknown_credit",
                RawReferenceType: "credit",
                Description: "unexpected positive credit",
                Amount: 42.50m,
                NetAmount: 42.50m,
                AmountTry: 2100m,
                Currency: "USD",
                CreatedAt: DateTimeOffset.UtcNow,
                ExchangeRate: 49.4m,
                ReferenceId: 12345L,
                EntryId: 999L)
        };

        var report = EtsyLedgerFinancialEngine.BuildReport(entries, new Dictionary<DateTime, decimal>());

        Assert.Equal(0m, report.GrossSales);
        Assert.NotNull(report.UnclassifiedPositiveWarning);
        Assert.Contains("unknown_credit", report.UnclassifiedPositiveWarning);
        Assert.Contains("42.50", report.UnclassifiedPositiveWarning);
    }

    [Fact]
    public async Task EtsyLedgerReportService_WhenLedgerHasNoSalesButPaidReceiptsExist_FallsBackToReceiptGrossSales()
    {
        var dataClient = new FailingOrSeededEtsyDataClient { ShouldThrow429 = false };
        var repo = new StubIntegrationRepository();
        var service = new EtsyLedgerReportService(dataClient, repo);

        var start = DateTimeOffset.UtcNow.AddDays(-5);
        var end = DateTimeOffset.UtcNow;
        var result = await service.GetLiveReportAsync("shop-fallback-gross", start, end);

        Assert.True(result.LedgerOk);
        Assert.Equal(45m, result.Performance.GrossSales);
        Assert.Equal(45m, result.Performance.OrderGrossSalesUSD);
        Assert.Equal(1, result.ReceiptCount);
        Assert.NotNull(result.Warning);
        Assert.Contains("ödenmiş sipariş fişlerinden", result.Warning);
    }

    [Fact]
    public async Task EtsyRateLimitStatusProvider_CapturesDailyLimitHeadersAndExceededState()
    {
        var shopId = $"shop-telemetry-{Guid.NewGuid():N}";
        var tokenStore = new InMemoryTokenStore(new EtsyOAuthToken(
            "valid-token",
            "refresh-1",
            DateTimeOffset.UtcNow.AddHours(1)));

        var innerHandler = new StubHttpMessageHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":\"Exceeded daily rate limit\"}")
            };
            resp.Headers.Add("x-limit-per-day", "5000");
            resp.Headers.Add("x-remaining-today", "0");
            resp.Headers.Add("x-limit-per-second", "5");
            resp.Headers.Add("x-remaining-this-second", "4");
            resp.Headers.Add("retry-after", "16043");
            return resp;
        });

        var handler = new EtsyAccessTokenHandler(tokenStore, new CountingOAuthService())
        {
            InnerHandler = innerHandler
        };

        using var invoker = new HttpMessageInvoker(handler);
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://openapi.etsy.com/v3/test");
        req.Headers.Add("X-Etsy-Shop-Id", shopId);
        using var res = await invoker.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);

        var provider = new EtsyRateLimitStatusProvider();
        var snap = provider.GetSnapshot(shopId);

        Assert.NotNull(snap);
        Assert.Equal(5000, snap.DailyLimit);
        Assert.Equal(0, snap.RemainingToday);
        Assert.Equal(5, snap.PerSecondLimit);
        Assert.Equal(4, snap.RemainingThisSecond);
        Assert.Equal(16043L, snap.RetryAfterSeconds);
        Assert.True(snap.IsDailyLimitExceeded);
    }

    [Fact]
    public async Task EtsyDashboardService_GetFinancialPerformance_FallsBackToStaleLiveSnapshotOn429()
    {
        var shopId = $"shop-perf-stale-{Guid.NewGuid():N}";
        var ledgerReport = new ToggleableLedgerReportService();
        var service = new EtsyDashboardService(
            new StubReportingService(),
            ledgerReport,
            new FailingOrSeededEtsyDataClient(),
            new StubIntegrationRepository(),
            new StubBankDepositService());

        ledgerReport.IsLiveOk = true;
        var warm = await service.GetFinancialPerformanceAsync(shopId, "this_month", null, null);
        Assert.True(warm.LedgerOk);
        Assert.Equal(320m, warm.GrossSales);

        ledgerReport.IsLiveOk = false;
        var stale = await service.GetFinancialPerformanceAsync(shopId, "this_month", null, null);
        Assert.False(stale.LedgerOk);
        Assert.Equal(320m, stale.GrossSales);
        Assert.Contains("son başarılı önbellek", stale.LedgerWarning);
    }

    private sealed class InMemoryTokenStore(EtsyOAuthToken initial) : IEtsyTokenStore
    {
        private EtsyOAuthToken _token = initial;

        public Task<EtsyOAuthToken?> GetAsync(string shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<EtsyOAuthToken?>(_token);

        public Task SaveAsync(string shopId, EtsyOAuthToken token, CancellationToken cancellationToken = default)
        {
            _token = token;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EtsyConnectedShopDto>> GetConnectedShopsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyConnectedShopDto>>(Array.Empty<EtsyConnectedShopDto>());
    }

    private sealed class CountingOAuthService : IEtsyOAuthService
    {
        public int RefreshCalls;

        public Task<string> CreateAuthorizationUrlAsync(string userId, string state, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EtsyOAuthConnectUrlResult> CreatePkceConnectUrlAsync(string shopId, string? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EtsyOAuthToken> ExchangeCodeAsync(string shopId, string code, string codeVerifier, string? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EtsyOAuthToken> ExchangeCodeWithPkceAsync(string shopId, string code, string? state = null, string? codeVerifier = null, string? redirectUri = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<EtsyOAuthToken> RefreshTokenAsync(string shopId, string refreshToken, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref RefreshCalls);
            await Task.Delay(50, cancellationToken);
            return new EtsyOAuthToken("new-access-token", "new-refresh-token", DateTimeOffset.UtcNow.AddHours(1));
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class FailingOrSeededEtsyDataClient : IEtsyDataClient
    {
        public bool ShouldThrow429 { get; set; }

        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());

        public Task<IReadOnlyList<EtsyFinancialTransaction>> GetFinancialTransactionsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyFinancialTransaction>>(Array.Empty<EtsyFinancialTransaction>());

        public Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyOrderCostAlert>>(Array.Empty<EtsyOrderCostAlert>());

        public Task<IReadOnlyList<EtsyDashboardReceipt>> GetShopReceiptsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow429)
                throw new HttpRequestException("Etsy API 429 (TooManyRequests): rate limit exceeded", null, HttpStatusCode.TooManyRequests);

            return Task.FromResult<IReadOnlyList<EtsyDashboardReceipt>>(new[]
            {
                new EtsyDashboardReceipt(
                    ReceiptId: 1001L,
                    CreatedAt: DateTimeOffset.UtcNow.AddHours(-2),
                    IsPaid: true,
                    IsCanceled: false,
                    IsFromOffsiteAds: false,
                    CurrencyCode: "USD",
                    GrandTotal: 45m,
                    Subtotal: 40m,
                    ShippingCost: 5m,
                    DiscountAmt: 0m,
                    TaxCost: 0m,
                    RefundedAmount: 0m,
                    Items: new[] { new EtsyDashboardReceiptItem("Test Product", 555L, 1, 9001L) },
                    BuyerName: "Test Buyer")
            });
        }

        public Task<IReadOnlyList<EtsyDashboardLedgerFee>> GetPaymentAccountLedgerEntriesAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyDashboardLedgerFee>>(Array.Empty<EtsyDashboardLedgerFee>());

        public Task<IReadOnlyList<EtsyLedgerEntryDetail>> GetLedgerEntriesDetailedAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyLedgerEntryDetail>>(Array.Empty<EtsyLedgerEntryDetail>());
    }

    private sealed class StubReportingService : IEtsyReportingService
    {
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());

        public Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FinancialPerformance(startDate, endDate, "USD", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m));

        public Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyOrderCostAlert>>(Array.Empty<EtsyOrderCostAlert>());

        public Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubLedgerReportService : IEtsyLedgerReportService
    {
        public Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EtsyLedgerLiveResult(
                LedgerOk: false,
                Warning: "Etsy API 429 (TooManyRequests)",
                Source: "etsy-unavailable",
                GeneratedAt: DateTimeOffset.UtcNow,
                Performance: new FinancialPerformance(startDate, endDate, "USD", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
                Daily: Array.Empty<EtsyLedgerDailySummary>(),
                ReceiptCount: 0));
    }

    private sealed class StubIntegrationRepository : IEtsyIntegrationRepository
    {
        public Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);
        public Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());
        public Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyMonthlyOrderSummary>>(Array.Empty<EtsyMonthlyOrderSummary>());
        public Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyListingTrafficRecord>>(Array.Empty<EtsyListingTrafficRecord>());
        public Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken cancellationToken = default) => Task.FromResult<EtsyChartSnapshot?>(null);
        public Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<long, EtsyDashboardOrderCost>>(new Dictionary<long, EtsyDashboardOrderCost>());
        public Task UpsertOrderCostAsync(string shopId, string orderId, decimal? productCost, decimal? shippingCost, decimal? packagingCost, string? notes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubBankDepositService : IBankDepositService
    {
        public MonthlyDepositSummary CalculateMonthlyDeposits(IEnumerable<RawDepositEntryInput> rawEntries, DateTimeOffset periodStart, DateTimeOffset periodEnd, Func<DateTime, decimal>? exchangeRateResolver = null, decimal defaultExchangeRate = 48.25m) =>
            throw new NotSupportedException();
    }

    private sealed class ToggleableLedgerReportService : IEtsyLedgerReportService
    {
        public bool IsLiveOk { get; set; }

        public Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
        {
            if (IsLiveOk)
            {
                return Task.FromResult(new EtsyLedgerLiveResult(
                    LedgerOk: true,
                    Warning: null,
                    Source: "etsy-ledger-live",
                    GeneratedAt: DateTimeOffset.UtcNow,
                    Performance: new FinancialPerformance(startDate, endDate, "USD", 320m, 40m, 10m, 0m, 60m, 0m, 0m, 210m, 65.6m, 16000m, 10500m),
                    Daily: Array.Empty<EtsyLedgerDailySummary>(),
                    ReceiptCount: 5));
            }

            return Task.FromResult(new EtsyLedgerLiveResult(
                LedgerOk: false,
                Warning: "Etsy API 429 (TooManyRequests)",
                Source: "etsy-unavailable",
                GeneratedAt: DateTimeOffset.UtcNow,
                Performance: new FinancialPerformance(startDate, endDate, "USD", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
                Daily: Array.Empty<EtsyLedgerDailySummary>(),
                ReceiptCount: 0));
        }
    }
}
