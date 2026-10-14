namespace EtsyMarketPlace.Application.MarketResearch;

using System;
using System.Collections.Generic;

/// <summary>
/// Etsy pazar araştırması için para birimi → USD dönüştürücü (yaklaşık sabit kur tablosu).
/// Etsy arama sonuçları satıcının para biriminde döner (USD, EUR, TRY, IDR...); KPI ve tahmin
/// motoru tek para biriminde (USD) çalıştığı için tüm fiyatlar burada normalize edilir.
/// TRY/EUR/GBP kurları projedeki mevcut motor değerleriyle uyumludur
/// (TRY ÷48.26 — HistoricalExchangeRateProvider; EUR ×1.1, GBP ×1.3 — EtsyApiClient defter dönüşümü).
/// Tablo tek noktadan güncellenir; bilinmeyen para birimi dönüştürülemez (null) ve
/// KPI ortalamalarına dahil edilmez.
/// </summary>
public static class MarketCurrencyConverter
{
    private static readonly Dictionary<string, decimal> RatesToUsd = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1.00m,
        ["EUR"] = 1.10m,
        ["GBP"] = 1.30m,
        ["TRY"] = 1.00m / 48.26m,
        ["CAD"] = 0.72m,
        ["AUD"] = 0.66m,
        ["CHF"] = 1.15m,
        ["HKD"] = 0.128m,
        ["JPY"] = 0.0066m,
        ["NZD"] = 0.60m,
        ["SEK"] = 0.095m,
        ["NOK"] = 0.092m,
        ["DKK"] = 0.145m,
        ["PLN"] = 0.25m,
        ["CZK"] = 0.043m,
        ["HUF"] = 0.0028m,
        ["RON"] = 0.215m,
        ["ILS"] = 0.27m,
        ["MXN"] = 0.057m,
        ["MYR"] = 0.215m,
        ["SGD"] = 0.74m,
        ["THB"] = 0.028m,
        ["PHP"] = 0.0172m,
        ["IDR"] = 0.0000615m,
        ["BRL"] = 0.19m,
        ["INR"] = 0.012m,
        ["ZAR"] = 0.054m,
    };

    /// <summary>Para birimi kur tablosunda tanımlı mı?</summary>
    public static bool IsSupported(string? currencyCode) =>
        !string.IsNullOrWhiteSpace(currencyCode) && RatesToUsd.ContainsKey(currencyCode.Trim());

    /// <summary>
    /// Tutarı (Etsy'nin döndürdüğü majör birimde) USD'ye çevirir.
    /// Bilinmeyen para birimi veya geçersiz (≤0) tutarda null döner.
    /// </summary>
    public static decimal? TryConvertToUsd(decimal amount, string? currencyCode)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        if (!RatesToUsd.TryGetValue(currencyCode.Trim(), out var rate))
        {
            return null;
        }

        return Math.Round(amount * rate, 2);
    }
}
