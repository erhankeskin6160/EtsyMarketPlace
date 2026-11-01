namespace EtsyMarketPlace.Api.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Api.Services;
using Xunit;

public class DashboardResponseCacheTests
{
    public DashboardResponseCacheTests()
    {
        DashboardResponseCache.Clear();
    }

    [Fact]
    public void TryGet_ReturnsCachedPayload_WithinTtl()
    {
        var key = "test-key-1";
        var payload = new { Name = "EtsyStore", Value = 1234.56m };

        DashboardResponseCache.Set(key, payload, TimeSpan.FromMinutes(2));

        var found = DashboardResponseCache.TryGet(key, out var cached);
        Assert.True(found);
        Assert.NotNull(cached);
        Assert.Same(payload, cached);
    }

    [Fact]
    public async Task TryGet_ReturnsFalse_WhenExpired()
    {
        var key = "test-key-expired";
        var payload = "expired-value";

        DashboardResponseCache.Set(key, payload, TimeSpan.FromMilliseconds(50));
        await Task.Delay(100);

        var found = DashboardResponseCache.TryGet(key, out var cached);
        Assert.False(found);
        Assert.Null(cached);
    }

    [Fact]
    public async Task GetOrAddAsync_DeduplicatesSimultaneousCalls_SingleFlight()
    {
        var key = "test-single-flight";
        var executionCounter = 0;

        // 10 adet eşzamanlı istek aynı anda çağrıldığında
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(DashboardResponseCache.GetOrAddAsync(
                key,
                TimeSpan.FromSeconds(5),
                async () =>
                {
                    Interlocked.Increment(ref executionCounter);
                    await Task.Delay(100); // Ağ çağrısını simüle et
                    return "single-flight-result";
                }));
        }

        var results = await Task.WhenAll(tasks);

        // Factory fonksiyonu SADECE 1 KEZ çalışmış olmalıdır!
        Assert.Equal(1, executionCounter);
        foreach (var r in results)
        {
            Assert.Equal("single-flight-result", r);
        }
    }

    [Fact]
    public async Task GetOrAddAsync_WithForceRefresh_BypassesCache()
    {
        var key = "test-force-refresh";
        var executionCounter = 0;

        var first = await DashboardResponseCache.GetOrAddAsync(
            key,
            TimeSpan.FromMinutes(2),
            () => Task.FromResult<string>($"val-{Interlocked.Increment(ref executionCounter)}"));

        Assert.Equal("val-1", first);
        Assert.Equal(1, executionCounter);

        // İkinci çağrı (forceRefresh: false) -> önbellekten dönmeli
        var cached = await DashboardResponseCache.GetOrAddAsync(
            key,
            TimeSpan.FromMinutes(2),
            () => Task.FromResult<string>($"val-{Interlocked.Increment(ref executionCounter)}"));

        Assert.Equal("val-1", cached);
        Assert.Equal(1, executionCounter);

        // Üçüncü çağrı (forceRefresh: true) -> önbelleği atlayıp taze veri getirmeli
        var refreshed = await DashboardResponseCache.GetOrAddAsync(
            key,
            TimeSpan.FromMinutes(2),
            () => Task.FromResult<string>($"val-{Interlocked.Increment(ref executionCounter)}"),
            forceRefresh: true);

        Assert.Equal("val-2", refreshed);
        Assert.Equal(2, executionCounter);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        DashboardResponseCache.Set("k1", "v1", TimeSpan.FromMinutes(1));
        DashboardResponseCache.Set("k2", "v2", TimeSpan.FromMinutes(1));

        DashboardResponseCache.Clear();

        Assert.False(DashboardResponseCache.TryGet("k1", out _));
        Assert.False(DashboardResponseCache.TryGet("k2", out _));
    }
}
