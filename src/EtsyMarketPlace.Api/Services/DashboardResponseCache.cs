using System.Collections.Concurrent;

namespace EtsyMarketPlace.Api.Services;

/// <summary>Kontrol paneli canli veri yanitlari icin kisa omurlu bellek ici onbellek (Etsy hiz limitlerini korur).</summary>
public static class DashboardResponseCache
{
    private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new();
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
        foreach (var staleKey in Entries.Where(kv => kv.Value.ExpiresAtUtc <= DateTimeOffset.UtcNow).Select(kv => kv.Key).ToList())
        {
            Entries.TryRemove(staleKey, out _);
        }

        Entries[key] = new CacheEntry(DateTimeOffset.UtcNow.Add(ttl), payload);
    }
}
