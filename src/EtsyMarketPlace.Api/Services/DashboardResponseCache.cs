using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace EtsyMarketPlace.Api.Services;

/// <summary>
/// Kontrol paneli canlı veri yanıtları için kısa ömürlü bellek içi önbellek ve In-Flight eşzamanlı istek tekilleştirici.
/// Etsy API hız limitlerini (429 Rate Limit) korur ve Cache Stampede (Thundering Herd) oluşmasını engeller.
/// </summary>
public static class DashboardResponseCache
{
    private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Task<object?>> InFlight = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CacheEntry(DateTimeOffset ExpiresAtUtc, object Payload);

    public static bool TryGet(string key, out object? payload)
    {
        if (Entries.TryGetValue(key, out var entry) && entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            payload = entry.Payload;
            return true;
        }

        payload = null;
        return false;
    }

    public static void Set(string key, object payload, TimeSpan ttl)
    {
        EvictExpired();
        Entries[key] = new CacheEntry(DateTimeOffset.UtcNow.Add(ttl), payload);
    }

    /// <summary>
    /// Eşzamanlı istekleri tekilleştiren (Single-Flight) ve önbellekten dönen jenerik çağırıcı.
    /// Aynı anahtar için aynı anda birden çok istek geldiğinde factory fonksiyonu yalnızca bir kez çalışır.
    /// </summary>
    public static async Task<T?> GetOrAddAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory, bool forceRefresh = false) where T : class
    {
        if (!forceRefresh && TryGet(key, out var cached) && cached is T typedCached)
        {
            return typedCached;
        }

        Task<object?> fetchTask;
        lock (InFlight)
        {
            if (!forceRefresh && TryGet(key, out var doubleChecked) && doubleChecked is T dcCached)
            {
                return dcCached;
            }

            if (!InFlight.TryGetValue(key, out fetchTask!))
            {
                fetchTask = Task.Run(async () =>
                {
                    try
                    {
                        var data = await factory();
                        if (data != null)
                        {
                            Set(key, data, ttl);
                        }
                        return (object?)data;
                    }
                    finally
                    {
                        InFlight.TryRemove(key, out _);
                    }
                });
                InFlight[key] = fetchTask;
            }
        }

        var result = await fetchTask;
        return result as T;
    }

    public static void Remove(string key)
    {
        Entries.TryRemove(key, out _);
        InFlight.TryRemove(key, out _);
    }

    public static void Clear()
    {
        Entries.Clear();
        InFlight.Clear();
    }

    private static void EvictExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var staleKeys = Entries
            .Where(kv => kv.Value.ExpiresAtUtc <= now)
            .Select(kv => kv.Key)
            .Take(50)
            .ToList();

        foreach (var staleKey in staleKeys)
        {
            Entries.TryRemove(staleKey, out _);
        }
    }
}
