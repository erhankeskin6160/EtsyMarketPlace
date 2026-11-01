namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RealProvider = EtsyMarketPlace.Application.EtsyIntegration.HistoricalExchangeRateProvider;

/// <summary>
/// WinForms geriye dönük uyumluluk adaptörü (Facade).
/// Tüm kur çözümlemelerini Clean Architecture Application katmanındaki tekil sağlayıcıya (Single Source of Truth) delege eder.
/// WinForms masaüstü ekranlarının canlı API kur tamamlama ve TCMB parite motorundan yararlanmasını sağlar.
/// </summary>
internal static class HistoricalExchangeRateProvider
{
    /// <summary>
    /// Belirtilen sipariş/işlem gününe ait Dolar/TL kilitli kurunu döndürür.
    /// </summary>
    public static decimal GetRateForDate(DateTime date, decimal fallbackRate = 48.25m)
        => RealProvider.GetRateForDate(date, fallbackRate);

    /// <summary>
    /// Belirli bir gün için özel kur ataması yapar.
    /// </summary>
    public static void SetCustomRate(DateTime date, decimal rate)
        => RealProvider.SetCustomRate(date, rate);

    /// <summary>
    /// Sipariş listesindeki tüm farklı tarihleri tek bir hamlede toplu olarak API'den çeker ve önbelleğe kilitler.
    /// </summary>
    public static Task HydrateRatesForDatesAsync(IEnumerable<DateTime> dates, CancellationToken ct = default)
        => RealProvider.HydrateRatesForDatesAsync(dates, ct);
}
