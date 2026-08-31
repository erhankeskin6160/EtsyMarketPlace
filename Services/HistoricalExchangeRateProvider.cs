namespace SimilarProductsWinForms.Services;

using System.Collections.Concurrent;

internal static class HistoricalExchangeRateProvider
{
    private static readonly ConcurrentDictionary<DateTime, decimal> RateCache = new();

    // Örnek bilinen dönemsel Dolar/TL kurları (TCMB / Piyasa Kapanış)
    private static readonly Dictionary<DateTime, decimal> KnownRates = new()
    {
        { new DateTime(2026, 1, 1),  46.80m },
        { new DateTime(2026, 3, 1),  47.20m },
        { new DateTime(2026, 5, 1),  47.65m },
        { new DateTime(2026, 7, 1),  48.00m },
        { new DateTime(2026, 8, 1),  48.20m },
        { new DateTime(2026, 8, 15), 48.25m },
        { new DateTime(2026, 8, 30), 48.26m },
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
