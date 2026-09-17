namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;

/// <summary>
/// Tüm yapay zeka sağlayıcıları için iki kademeli (In-Memory + SQLite) akıllı önbellekleme
/// ve delta senkronizasyon servisi. Gereksiz API sorgularını önler, panel geçişlerini 0 ms'ye indirir.
/// </summary>
public static class AiDataCacheService
{
    private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt);

    private static readonly ConcurrentDictionary<string, CacheEntry> MemoryCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Bir yapay zeka işlemi gerçekleşip yerel sayaca yazıldığında tetiklenen olay.
    /// </summary>
    public static event Action? OnUsageUpdated;

    /// <summary>
    /// Model isimleri ve kotalar için 24 saatlik önbellek süresi.
    /// </summary>
    public static readonly TimeSpan ModelsCacheTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// Canlı bakiye ve anlık sorgular için varsayılan 5 dakikalık önbellek süresi.
    /// </summary>
    public static readonly TimeSpan BalanceCacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Belirtilen anahtar için önce belleğe, sonra SQLite'a bakar. Yoksa fetcher çalıştırıp iki katmana da kaydeder.
    /// </summary>
    public static async Task<T> GetOrFetchAsync<T>(
        string provider,
        string cacheKey,
        string category,
        TimeSpan ttl,
        bool isFinalized,
        Func<Task<T>> fetcher,
        bool forceRefresh = false,
        CancellationToken ct = default) where T : class
    {
        string fullKey = $"{provider}:{cacheKey}";

        // 1. In-Memory Cache Kontrolü (forceRefresh değilse)
        if (!forceRefresh && MemoryCache.TryGetValue(fullKey, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            if (entry.Value is T typedVal) return typedVal;
        }

        var repo = AiTokenUsageTrackerService.GetRepository();

        // 2. SQLite Persistent Cache Kontrolü
        if (!forceRefresh)
        {
            try
            {
                var cachedJson = await repo.GetCachedPayloadAsync(provider, cacheKey, ct);
                if (!string.IsNullOrWhiteSpace(cachedJson))
                {
                    var deserialized = JsonSerializer.Deserialize<T>(cachedJson);
                    if (deserialized != null)
                    {
                        // Bellek önbelleğini de tazele
                        MemoryCache[fullKey] = new CacheEntry(deserialized, DateTimeOffset.UtcNow.Add(ttl));
                        return deserialized;
                    }
                }
            }
            catch
            {
                // SQLite hatası durumunda fetcher'a devam et
            }
        }

        // 3. Uzak API'den Taze Veri Çekimi
        var freshData = await fetcher();
        if (freshData != null)
        {
            // Belleğe yaz
            MemoryCache[fullKey] = new CacheEntry(freshData, DateTimeOffset.UtcNow.Add(ttl));

            // SQLite'a kaydet
            try
            {
                var json = JsonSerializer.Serialize(freshData);
                DateTimeOffset? expiresAt = isFinalized ? null : DateTimeOffset.UtcNow.Add(ttl);
                await repo.SaveCachedPayloadAsync(provider, cacheKey, category, json, isFinalized, expiresAt, ct);
            }
            catch
            {
                // Kaydetme hatası akışı durdurmaz
            }
        }

        return freshData;
    }

    /// <summary>
    /// Program içi bir AI harcaması yapıldığında çağrılır; bakiye ve bugünkü kullanım önbelleklerini geçersiz kılar.
    /// </summary>
    public static void InvalidateTodayAndBalance(string? provider = null)
    {
        string todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Bellekten temizle
        foreach (var key in MemoryCache.Keys)
        {
            if (key.Contains("balance", StringComparison.OrdinalIgnoreCase) ||
                key.Contains(todayKey, StringComparison.OrdinalIgnoreCase) ||
                (provider != null && key.StartsWith(provider, StringComparison.OrdinalIgnoreCase)))
            {
                MemoryCache.TryRemove(key, out _);
            }
        }

        // SQLite'tan bugünkü geçici kaydı temizle
        _ = Task.Run(async () =>
        {
            try
            {
                var repo = AiTokenUsageTrackerService.GetRepository();
                await repo.InvalidateCacheAsync(provider, todayKey);
            }
            catch { }
        });

        OnUsageUpdated?.Invoke();
    }

    /// <summary>
    /// Tüm bellek önbelleğini temizler.
    /// </summary>
    public static void ClearAllMemory()
    {
        MemoryCache.Clear();
    }
}
