using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.EtsyIntegration;

public sealed class EtsyDbFirstDeltaSyncTests : IAsyncLifetime
{
    private readonly string _tempDbPath;
    private readonly SqliteEtsyIntegrationStore _store;

    public EtsyDbFirstDeltaSyncTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"etsy-dbfirst-test-{Guid.NewGuid():N}.db");
        var options = Options.Create(new EtsyIntegrationOptions { DatabasePath = _tempDbPath });
        _store = new SqliteEtsyIntegrationStore(options, new DummyDataProtector());
    }

    public Task InitializeAsync() => _store.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task GetLedgerEntriesDetailedAsync_SecondCallWithinInterval_ServesFromSqliteWithZeroHttpCalls()
    {
        var shopId = $"shop-db-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-5);
        var entryCreatedUnix = start.AddDays(1).ToUnixTimeSeconds();

        var httpCalls = 0;
        var handler = new RecordingHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref httpCalls);
            var json = $"{{\"results\":[{{\"entry_id\":1001,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Sale #1\",\"amount\":5000,\"currency_code\":\"USD\",\"create_date\":{entryCreatedUnix}}}]}}";
            return OkJson(json);
        });

        var client1 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var first = await client1.GetLedgerEntriesDetailedAsync(shopId, start, now);

        Assert.Equal(1, httpCalls);
        Assert.Single(first);
        Assert.Equal(50m, first[0].Amount);

        // Farklı ApiBaseUrl ile ikinci client oluşturularak RAM cache'i atlanır ve doğrudan SQLite'tan okuma doğrulanır.
        var client2 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var second = await client2.GetLedgerEntriesDetailedAsync(shopId, start, now);

        Assert.Equal(1, httpCalls);
        Assert.Single(second);
        Assert.Equal(50m, second[0].Amount);
    }

    [Fact]
    public async Task GetLedgerEntriesDetailedAsync_WhenForceRefreshTriggered_FetchesOnlyDeltaAndMerges()
    {
        var shopId = $"shop-delta-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-10);
        var initialEntryUnix = start.AddDays(1).ToUnixTimeSeconds();
        var deltaEntryUnix = now.AddSeconds(-10).ToUnixTimeSeconds();

        var callIndex = 0;
        var handler = new RecordingHttpMessageHandler(_ =>
        {
            var idx = Interlocked.Increment(ref callIndex);
            var json = idx == 1
                ? $"{{\"results\":[{{\"entry_id\":1001,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Initial\",\"amount\":5000,\"currency_code\":\"USD\",\"create_date\":{initialEntryUnix}}}]}}"
                : $"{{\"results\":[{{\"entry_id\":1002,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Delta\",\"amount\":7500,\"currency_code\":\"USD\",\"create_date\":{deltaEntryUnix}}}]}}";
            return OkJson(json);
        });

        var client = CreateClient(
            handler,
            $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/",
            forceRefreshCooldownSeconds: 0);

        var initial = await client.GetLedgerEntriesDetailedAsync(shopId, start, now);
        Assert.Single(initial);
        Assert.Single(handler.Requests);

        var watermark = await _store.GetWatermarkAsync(shopId, $"ledger_active:{start.ToUnixTimeSeconds()}");
        Assert.NotNull(watermark);

        IReadOnlyList<EtsyLedgerEntryDetail> merged;
        using (EtsySyncContext.BeginForceRefresh())
        {
            merged = await client.GetLedgerEntriesDetailedAsync(shopId, start, DateTimeOffset.UtcNow);
        }

        Assert.Equal(2, handler.Requests.Count);
        var secondQuery = ParseQuery(handler.Requests[1].RequestUri!);
        Assert.True(long.TryParse(secondQuery["min_created"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var deltaMinCreated));
        Assert.True(deltaMinCreated > start.ToUnixTimeSeconds());
        Assert.Equal(Math.Max(start.ToUnixTimeSeconds(), watermark!.MaxSyncedUnix - 300), deltaMinCreated);

        Assert.Equal(2, merged.Count);
        Assert.Equal(125m, merged.Sum(x => x.Amount));
    }

    [Fact]
    public async Task GetLedgerEntriesDetailedAsync_WhenDeltaSyncHits429_ReturnsCachedSqliteEntries()
    {
        var shopId = $"shop-429-sqlite-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-7);
        var entryUnix = start.AddDays(2).ToUnixTimeSeconds();
        var shouldFailWith429 = false;

        var handler = new RecordingHttpMessageHandler(_ =>
        {
            if (shouldFailWith429)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\":\"Exceeded daily rate limit\"}", Encoding.UTF8, "application/json")
                };
            }

            return OkJson($"{{\"results\":[{{\"entry_id\":2001,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Cached Sale\",\"amount\":4200,\"currency_code\":\"USD\",\"create_date\":{entryUnix}}}]}}");
        });

        var client1 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/", forceRefreshCooldownSeconds: 0);
        var seed = await client1.GetLedgerEntriesDetailedAsync(shopId, start, now);
        Assert.Single(seed);

        shouldFailWith429 = true;
        var client2 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/", forceRefreshCooldownSeconds: 0);

        IReadOnlyList<EtsyLedgerEntryDetail> fallbackResult;
        using (EtsySyncContext.BeginForceRefresh())
        {
            fallbackResult = await client2.GetLedgerEntriesDetailedAsync(shopId, start, DateTimeOffset.UtcNow);
        }

        Assert.Single(fallbackResult);
        Assert.Equal(42m, fallbackResult[0].Amount);
    }

    [Fact]
    public async Task GetReceiptIdsByLedgerEntriesAsync_CachesMappingsInSqlite_AvoidsRepeatHttpCalls()
    {
        var shopId = $"shop-map-{Guid.NewGuid():N}";
        var httpCalls = 0;

        var handler = new RecordingHttpMessageHandler(req =>
        {
            Interlocked.Increment(ref httpCalls);
            var query = ParseQuery(req.RequestUri!);
            var entryId = long.Parse(query["ledger_entry_ids"], CultureInfo.InvariantCulture);
            var receiptId = entryId switch
            {
                3001L => 99001L,
                3002L => 99002L,
                3003L => 99003L,
                _ => 0L
            };
            return OkJson($"{{\"results\":[{{\"receipt_id\":{receiptId}}}]}}");
        });

        var client = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");

        var first = await client.GetReceiptIdsByLedgerEntriesAsync(shopId, new[] { 3001L, 3002L });
        Assert.Equal(2, httpCalls);
        Assert.Equal(99001L, first[3001L]);
        Assert.Equal(99002L, first[3002L]);

        var second = await client.GetReceiptIdsByLedgerEntriesAsync(shopId, new[] { 3001L, 3002L, 3003L });
        Assert.Equal(3, httpCalls);
        Assert.Equal(99001L, second[3001L]);
        Assert.Equal(99002L, second[3002L]);
        Assert.Equal(99003L, second[3003L]);
    }

    [Fact]
    public async Task GetShopReceiptsAsync_And_GetReceiptByIdAsync_ServesFromSqliteWithZeroExtraHttpCalls()
    {
        var shopId = $"shop-rcpt-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-5);
        var createdUnix = start.AddDays(1).ToUnixTimeSeconds();
        var httpCalls = 0;

        var handler = new RecordingHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref httpCalls);
            return OkJson($"{{\"results\":[{{\"receipt_id\":55001,\"created_timestamp\":{createdUnix},\"updated_timestamp\":{createdUnix},\"is_paid\":true,\"is_canceled\":false,\"status\":\"Paid\",\"name\":\"Alice\",\"currency_code\":\"USD\",\"grandtotal\":{{\"amount\":6500,\"divisor\":100}},\"subtotal\":{{\"amount\":6000,\"divisor\":100}},\"total_shipping_cost\":{{\"amount\":500,\"divisor\":100}},\"discount_amt\":{{\"amount\":0,\"divisor\":100}},\"total_tax_cost\":{{\"amount\":0,\"divisor\":100}},\"transactions\":[{{\"transaction_id\":7001,\"listing_id\":8001,\"title\":\"Lamp\",\"quantity\":1}}]}}]}}");
        });

        var client1 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var receipts = await client1.GetShopReceiptsAsync(shopId, start, now);
        Assert.Equal(1, httpCalls);
        Assert.Single(receipts);
        Assert.Equal(55001L, receipts[0].ReceiptId);

        var client2 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var receiptsFromDb = await client2.GetShopReceiptsAsync(shopId, start, now);
        var singleReceiptFromDb = await client2.GetReceiptByIdAsync(shopId, 55001L);

        Assert.Equal(1, httpCalls);
        Assert.Single(receiptsFromDb);
        Assert.NotNull(singleReceiptFromDb);
        Assert.Equal(65m, singleReceiptFromDb!.GrandTotal);
    }

    [Fact]
    public async Task GetLedgerEntriesDetailedAsync_NarrowRangeDeltaSync_PreservesWiderRollingMinSyncedUnix()
    {
        var shopId = $"shop-narrow-{Guid.NewGuid():N}";
        var baseUrl = $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/";
        var now = DateTimeOffset.UtcNow;
        var wideStart = now.AddDays(-12);
        var narrowStart = now.AddDays(-1);
        var oldEntryUnix = wideStart.AddDays(1).ToUnixTimeSeconds();
        var recentEntryUnix = narrowStart.AddHours(1).ToUnixTimeSeconds();

        var callCount = 0;
        var handler = new RecordingHttpMessageHandler(_ =>
        {
            var idx = Interlocked.Increment(ref callCount);
            if (idx == 1)
            {
                return OkJson($"{{\"results\":[{{\"entry_id\":4001,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Old\",\"amount\":3000,\"currency_code\":\"USD\",\"create_date\":{oldEntryUnix}}}]}}");
            }
            return OkJson($"{{\"results\":[{{\"entry_id\":4002,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Today\",\"amount\":2000,\"currency_code\":\"USD\",\"create_date\":{recentEntryUnix}}}]}}");
        });

        var client1 = CreateClient(handler, baseUrl, forceRefreshCooldownSeconds: 0);
        var initial = await client1.GetLedgerEntriesDetailedAsync(shopId, wideStart, now);
        Assert.Single(initial);

        // Dar aralıklı (daily-brief gibi) ForceDeltaRefresh sonrası rolling MinSyncedUnix daraltılmamalıdır.
        var client2 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/", forceRefreshCooldownSeconds: 0);
        using (EtsySyncContext.BeginForceRefresh())
        {
            var narrow = await client2.GetLedgerEntriesDetailedAsync(shopId, narrowStart, DateTimeOffset.UtcNow);
            Assert.Single(narrow);
        }

        var rolling = await _store.GetWatermarkAsync(shopId, "ledger_active:latest");
        Assert.NotNull(rolling);
        Assert.Equal(wideStart.ToUnixTimeSeconds(), rolling!.MinSyncedUnix);

        // Tekrar geniş pencere sorulduğunda 0 ekstra HTTP çağrısı ile her iki kayıt da DB'den dönmelidir.
        var client3 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/", deltaSyncIntervalMinutes: 30);
        var reRead = await client3.GetLedgerEntriesDetailedAsync(shopId, wideStart, now);
        Assert.Equal(2, callCount);
        Assert.Equal(2, reRead.Count);
        Assert.Equal(50m, reRead.Sum(x => x.Amount));
    }

    [Fact]
    public async Task GetLedgerEntriesDetailedAsync_LeftGapExtension_FetchesOnlyMissingLeftRange()
    {
        var shopId = $"shop-leftgap-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var recentStart = now.AddDays(-5);
        var widerStart = now.AddDays(-14);
        var recentEntryUnix = recentStart.AddDays(2).ToUnixTimeSeconds();
        var olderEntryUnix = widerStart.AddDays(3).ToUnixTimeSeconds();

        var handler = new RecordingHttpMessageHandler(req =>
        {
            var q = ParseQuery(req.RequestUri!);
            var minCreated = long.Parse(q["min_created"], CultureInfo.InvariantCulture);
            if (minCreated >= recentStart.ToUnixTimeSeconds() - 10)
            {
                return OkJson($"{{\"results\":[{{\"entry_id\":5001,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Recent\",\"amount\":4000,\"currency_code\":\"USD\",\"create_date\":{recentEntryUnix}}}]}}");
            }
            return OkJson($"{{\"results\":[{{\"entry_id\":5002,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Older\",\"amount\":6000,\"currency_code\":\"USD\",\"create_date\":{olderEntryUnix}}}]}}");
        });

        var client1 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/", deltaSyncIntervalMinutes: 30);
        var first = await client1.GetLedgerEntriesDetailedAsync(shopId, recentStart, now);
        Assert.Single(first);
        Assert.Single(handler.Requests);

        // Aynı cooldown penceresi içinde daha eski bir başlangıç tarihi (-14d) istendiğinde
        // sadece eksik sol boşluk [-14d .. -5d] çekilmeli, [-5d .. now] tekrar çekilmemelidir.
        var client2 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/", deltaSyncIntervalMinutes: 30);
        var extended = await client2.GetLedgerEntriesDetailedAsync(shopId, widerStart, now);

        Assert.Equal(2, handler.Requests.Count);
        var leftGapQuery = ParseQuery(handler.Requests[1].RequestUri!);
        var fetchedMin = long.Parse(leftGapQuery["min_created"], CultureInfo.InvariantCulture);
        var fetchedMax = long.Parse(leftGapQuery["max_created"], CultureInfo.InvariantCulture);
        Assert.Equal(widerStart.ToUnixTimeSeconds(), fetchedMin);
        Assert.Equal(recentStart.ToUnixTimeSeconds(), fetchedMax);

        Assert.Equal(2, extended.Count);
        Assert.Equal(100m, extended.Sum(x => x.Amount));
    }

    [Fact]
    public async Task GetLedgerEntriesDetailedAsync_ParallelRequestsWithDifferentStartDates_SerializeAndReuseRollingWatermark()
    {
        var shopId = $"shop-parallel-{Guid.NewGuid():N}";
        var baseUrl = $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/";
        var now = DateTimeOffset.UtcNow;
        var wideStart = now.AddDays(-15);
        var entryUnix = now.AddDays(-2).ToUnixTimeSeconds();

        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var httpCalls = 0;

        var handler = new RecordingHttpMessageHandler(async _ =>
        {
            var idx = Interlocked.Increment(ref httpCalls);
            if (idx == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.Task;
            }
            return OkJson($"{{\"results\":[{{\"entry_id\":6001,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"Parallel\",\"amount\":8500,\"currency_code\":\"USD\",\"create_date\":{entryUnix}}}]}}");
        });

        var client = CreateClient(handler, baseUrl, deltaSyncIntervalMinutes: 30);

        var firstTask = client.GetLedgerEntriesDetailedAsync(shopId, wideStart, now);
        await firstEntered.Task;

        // İlk geniş senkronizasyon devam ederken 3 farklı dar başlangıç tarihi paralel başlar.
        var t2 = client.GetLedgerEntriesDetailedAsync(shopId, now.AddDays(-10), now);
        var t3 = client.GetLedgerEntriesDetailedAsync(shopId, now.AddDays(-5), now);
        var t4 = client.GetLedgerEntriesDetailedAsync(shopId, now.AddDays(-1), now);

        await Task.Delay(50);
        releaseFirst.TrySetResult();

        var results = await Task.WhenAll(firstTask, t2, t3, t4);

        Assert.Equal(1, httpCalls);
        Assert.Single(results[0]);
        Assert.Single(results[1]);
        Assert.Single(results[2]);
        Assert.Empty(results[3]); // -2d tarihli kayıt -1d penceresinde olmamalı
    }

    [Fact]
    public async Task GetUnfulfilledCostAlertsAsync_UsesIncludesTransactionsAndDoesNotPoisonReceiptCache()
    {
        var shopId = $"shop-alerts-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-5);
        var createdUnix = start.AddDays(1).ToUnixTimeSeconds();

        var handler = new RecordingHttpMessageHandler(_ =>
            OkJson($"{{\"results\":[{{\"receipt_id\":77001,\"created_timestamp\":{createdUnix},\"updated_timestamp\":{createdUnix},\"is_paid\":true,\"is_canceled\":false,\"status\":\"Paid\",\"name\":\"Bob\",\"currency_code\":\"USD\",\"grandtotal\":{{\"amount\":5000,\"divisor\":100}},\"subtotal\":{{\"amount\":4500,\"divisor\":100}},\"total_shipping_cost\":{{\"amount\":500,\"divisor\":100}},\"discount_amt\":{{\"amount\":0,\"divisor\":100}},\"total_tax_cost\":{{\"amount\":0,\"divisor\":100}},\"transactions\":[{{\"transaction_id\":7701,\"listing_id\":8801,\"title\":\"Intact Lamp\",\"quantity\":2}}]}}]}}"));

        var client1 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var initial = await client1.GetShopReceiptsAsync(shopId, start, now);
        Assert.Single(initial);
        Assert.Single(initial[0].Items);
        Assert.Equal("Intact Lamp", initial[0].Items[0].Title);

        var client2 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var alerts = await client2.GetUnfulfilledCostAlertsAsync(shopId);
        Assert.Contains(handler.Requests, r =>
            r.RequestUri!.ToString().Contains("includes=Transactions", StringComparison.OrdinalIgnoreCase));

        // Receipt cache üzerindeki transactions detayı zehirlenmeden korunmalıdır.
        var client3 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var fromCache = await client3.GetReceiptByIdAsync(shopId, 77001L);
        Assert.NotNull(fromCache);
        Assert.Single(fromCache!.Items);
        Assert.Equal("Intact Lamp", fromCache.Items[0].Title);
        Assert.Equal(2, fromCache.Items[0].Quantity);
    }

    [Fact]
    public async Task GetReceiptsModifiedBetweenAsync_UpsertsModifiedReceiptsIntoSqliteCache()
    {
        var shopId = $"shop-modrcpt-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-5);
        var createdUnix = start.AddDays(1).ToUnixTimeSeconds();
        var isCanceledResponse = false;

        var handler = new RecordingHttpMessageHandler(_ =>
        {
            var canceledFlag = isCanceledResponse ? "true" : "false";
            return OkJson($"{{\"results\":[{{\"receipt_id\":88001,\"created_timestamp\":{createdUnix},\"updated_timestamp\":{now.ToUnixTimeSeconds()},\"is_paid\":true,\"is_canceled\":{canceledFlag},\"status\":\"Updated\",\"name\":\"Cara\",\"currency_code\":\"USD\",\"grandtotal\":{{\"amount\":6500,\"divisor\":100}},\"subtotal\":{{\"amount\":6000,\"divisor\":100}},\"total_shipping_cost\":{{\"amount\":500,\"divisor\":100}},\"discount_amt\":{{\"amount\":0,\"divisor\":100}},\"total_tax_cost\":{{\"amount\":0,\"divisor\":100}},\"transactions\":[{{\"transaction_id\":8801,\"listing_id\":9901,\"title\":\"Vase\",\"quantity\":1}}]}}]}}");
        });

        var client1 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var first = await client1.GetShopReceiptsAsync(shopId, start, now);
        Assert.False(first[0].IsCanceled);

        isCanceledResponse = true;
        var modified = await client1.GetReceiptsModifiedBetweenAsync(shopId, now.AddHours(-1), now);
        Assert.Single(modified);
        Assert.True(modified[0].IsCanceled);

        var countBefore = handler.Requests.Count;
        var client2 = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var cachedUpdated = await client2.GetReceiptByIdAsync(shopId, 88001L);

        Assert.Equal(countBefore, handler.Requests.Count);
        Assert.NotNull(cachedUpdated);
        Assert.True(cachedUpdated!.IsCanceled);
    }

    [Fact]
    public async Task GetLedgerEntriesDetailedAsync_MultiWindowOver30Days_DoesNotSkipActiveRecentWindow()
    {
        var shopId = $"shop-35d-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var start35d = now.AddDays(-35);
        var olderUnix = now.AddDays(-32).ToUnixTimeSeconds();
        var todayUnix = now.AddMinutes(-10).ToUnixTimeSeconds();

        var handler = new RecordingHttpMessageHandler(req =>
        {
            var q = ParseQuery(req.RequestUri!);
            var minCreated = long.Parse(q["min_created"], CultureInfo.InvariantCulture);
            if (minCreated < now.AddDays(-10).ToUnixTimeSeconds())
            {
                return OkJson($"{{\"results\":[{{\"entry_id\":9001,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"MonthOld\",\"amount\":3000,\"currency_code\":\"USD\",\"create_date\":{olderUnix}}}]}}");
            }
            return OkJson($"{{\"results\":[{{\"entry_id\":9002,\"ledger_type\":\"sale\",\"reference_type\":\"receipt\",\"description\":\"TodaySale\",\"amount\":7000,\"currency_code\":\"USD\",\"create_date\":{todayUnix}}}]}}");
        });

        var client = CreateClient(handler, $"https://mock-{Guid.NewGuid():N}.etsy.test/v3/");
        var entries = await client.GetLedgerEntriesDetailedAsync(shopId, start35d, now);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(2, entries.Count);
        Assert.Equal(100m, entries.Sum(x => x.Amount));
    }

    private EtsyApiClient CreateClient(
        HttpMessageHandler handler,
        string apiBaseUrl,
        int deltaSyncIntervalMinutes = 30,
        int forceRefreshCooldownSeconds = 120)
    {
        var httpClient = new HttpClient(handler);
        var apiOptions = Options.Create(new EtsyApiOptions
        {
            ApiKey = "test-key",
            SharedSecret = "test-secret",
            ApiBaseUrl = apiBaseUrl,
            DeltaSyncIntervalMinutes = deltaSyncIntervalMinutes,
            ForceRefreshCooldownSeconds = forceRefreshCooldownSeconds
        });

        return new EtsyApiClient(httpClient, apiOptions, _store, _store);
    }

    private static HttpResponseMessage OkJson(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static Dictionary<string, string> ParseQuery(Uri uri)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
            {
                dict[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
            }
        }
        return dict;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _asyncResponder;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _asyncResponder = req => Task.FromResult(responder(req));
        }

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncResponder)
        {
            _asyncResponder = asyncResponder;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request);
            }
            return _asyncResponder(request);
        }
    }

    private sealed class DummyDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => plaintext;
        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
