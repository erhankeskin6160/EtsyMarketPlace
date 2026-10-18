namespace SimilarProductsWinForms.Services;

using System.Collections.Concurrent;

internal static class HistoricalExchangeRateProvider
{
    private static readonly ConcurrentDictionary<DateTime, decimal> RateCache = new();

    // 2026 yılı dönemsel ve günlük resmi Dolar/TL kurları (TCMB / Piyasa Kapanış Kalibrasyonu)
    private static readonly Dictionary<DateTime, decimal> KnownRates = new()
    {
        { new DateTime(2026, 1, 1),  46.80m },
        { new DateTime(2026, 3, 1),  47.20m },
        { new DateTime(2026, 5, 1),  47.65m },
        { new DateTime(2026, 7, 1),  48.00m },
        { new DateTime(2026, 8, 1),  48.20m },
        { new DateTime(2026, 8, 15), 48.25m },
        { new DateTime(2026, 8, 30), 48.26m },

        // Eylül 2026 Günlük Kurları
        { new DateTime(2026, 9, 1),  48.28m },
        { new DateTime(2026, 9, 5),  48.35m },
        { new DateTime(2026, 9, 10), 48.50m },
        { new DateTime(2026, 9, 15), 48.64m },
        { new DateTime(2026, 9, 18), 48.73m },
        { new DateTime(2026, 9, 20), 48.72m },
        { new DateTime(2026, 9, 22), 48.78m },
        { new DateTime(2026, 9, 25), 48.82m },
        { new DateTime(2026, 9, 26), 48.85m },
        { new DateTime(2026, 9, 28), 48.88m },
        { new DateTime(2026, 9, 30), 48.95m },

        // Ekim 2026 Günlük Kurları (Frankfurter / Piyasa Kapanışları)
        { new DateTime(2026, 10, 1),  49.03m },
        { new DateTime(2026, 10, 2),  49.15m },
        { new DateTime(2026, 10, 3),  49.15m },
        { new DateTime(2026, 10, 4),  49.15m },
        { new DateTime(2026, 10, 5),  49.16m },
        { new DateTime(2026, 10, 6),  49.18m },
        { new DateTime(2026, 10, 7),  49.19m },
        { new DateTime(2026, 10, 8),  49.22m },
        { new DateTime(2026, 10, 9),  49.17m },
        { new DateTime(2026, 10, 10), 49.17m },
        { new DateTime(2026, 10, 11), 49.17m },
        { new DateTime(2026, 10, 12), 49.34m },
        { new DateTime(2026, 10, 13), 49.37m },
        { new DateTime(2026, 10, 14), 49.39m },
        { new DateTime(2026, 10, 15), 49.42m },
        { new DateTime(2026, 10, 16), 49.53m },
        { new DateTime(2026, 10, 17), 49.53m },
        { new DateTime(2026, 10, 18), 49.53m }
    };

    /// <summary>
    /// Belirtilen sipariş/işlem gününe ait Dolar/TL kurunu döndürür.
    /// </summary>
    public static decimal GetRateForDate(DateTime date, decimal fallbackRate = 48.25m)
    {
        var targetDate = date.Date;

        if (RateCache.TryGetValue(targetDate, out var cachedRate))
        {
            return cachedRate;
        }

        if (KnownRates.TryGetValue(targetDate, out var exactRate))
        {
            RateCache[targetDate] = exactRate;
            return exactRate;
        }

        // En yakın geçmiş bilinen kuru bul
        var pastRate = KnownRates
            .Where(kvp => kvp.Key <= targetDate)
            .OrderByDescending(kvp => kvp.Key)
            .Select(kvp => (decimal?)kvp.Value)
            .FirstOrDefault();

        var resolved = pastRate ?? fallbackRate;
        RateCache[targetDate] = resolved;
        return resolved;
    }

    public static void SetCustomRate(DateTime date, decimal rate)
    {
        if (rate > 0)
        {
            RateCache[date.Date] = rate;
            KnownRates[date.Date] = rate;
        }
    }
}
