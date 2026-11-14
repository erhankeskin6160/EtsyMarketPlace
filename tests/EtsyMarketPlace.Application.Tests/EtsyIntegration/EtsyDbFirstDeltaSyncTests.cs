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

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class DummyDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => plaintext;
        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
