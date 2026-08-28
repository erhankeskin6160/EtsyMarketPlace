namespace SimilarProductsWinForms.Services;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal sealed class ExchangeRateService
{
    private static readonly HttpClient _httpClient = new();
    private readonly SqliteExchangeRateRepository _repository = new();

    private static decimal? _cachedLiveRate;
    private static DateTime _lastLiveRateFetchTime = DateTime.MinValue;

    /// <summary>
    /// Anlık canlı USD/TRY kurunu Open ER-API ve Frankfurter API üzerinden çeker (15 dk in-memory cache).
    /// </summary>
    public async Task<decimal> GetLiveUsdTryRateAsync(CancellationToken ct = default)
    {
        // 1. 15 dakikalık in-memory önbellek kontrolü
        if (_cachedLiveRate.HasValue && (DateTime.UtcNow - _lastLiveRateFetchTime).TotalMinutes < 15)
        {
            return _cachedLiveRate.Value;
        }

        // 2. Birincil Canlı API: Open Exchange Rates public endpoint (open.er-api.com)
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://open.er-api.com/v6/latest/USD");
            using var response = await _httpClient.SendAsync(req, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                    rates.TryGetProperty("TRY", out var tryRate))
                {
                    decimal rate = tryRate.GetDecimal();
                    if (rate > 20m)
                    {
                        _cachedLiveRate = Math.Round(rate, 2);
                        _lastLiveRateFetchTime = DateTime.UtcNow;
                        return _cachedLiveRate.Value;
                    }
                }
            }
        }
        catch
        {
            // Birincil API başarısızsa ikincil API'ye geç
        }

        // 3. İkincil Canlı API: Frankfurter API (api.frankfurter.app)
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.frankfurter.app/latest?from=USD&to=TRY");
            using var response = await _httpClient.SendAsync(req, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                    rates.TryGetProperty("TRY", out var tryRate))
                {
                    decimal rate = tryRate.GetDecimal();
                    if (rate > 20m)
                    {
                        _cachedLiveRate = Math.Round(rate, 2);
                        _lastLiveRateFetchTime = DateTime.UtcNow;
                        return _cachedLiveRate.Value;
                    }
                }
            }
        }
        catch
        {
            // İkincil API de başarısızsa fallback
        }

        // 4. API'lere ulaşılamazsa bilinen en son kuru veya HistoricalExchangeRateProvider'ı kullan
        var fallback = _cachedLiveRate ?? HistoricalExchangeRateProvider.GetRateForDate(DateTime.Today, 36.55m);
        return fallback;
    }

    public async Task<decimal> GetHistoricalRateAsync(DateTime date, CancellationToken ct = default)
    {
        // En geç bugünü kullanabiliriz, yarına ait kur olmaz
        if (date.Date > DateTime.UtcNow.Date) date = DateTime.UtcNow.Date;

        // 1. Önce önbellekte (veritabanında) geçerli bir kur var mı bakıyoruz (25-55 TL aralığında)
        var cachedRate = await _repository.GetRateAsync(date.Date, ct);
        if (cachedRate.HasValue && cachedRate.Value >= 25m && cachedRate.Value <= 55m)
        {
            return cachedRate.Value;
        }

        // 2. Yoksa API'den çekiyoruz
        try
        {
            string dateStr = date.ToString("yyyy-MM-dd");
            string url = $"https://api.frankfurter.app/{dateStr}?from=USD&to=TRY";
            
            using var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("rates", out var ratesElement) &&
                    ratesElement.TryGetProperty("TRY", out var tryRateElement))
                {
                    decimal rate = tryRateElement.GetDecimal();
                    if (rate >= 25m && rate <= 55m)
                    {
                        await _repository.SaveRateAsync(date.Date, rate, ct);
                        return rate;
                    }
                }
            }
        }
        catch
        {
            // API hatası durumunda tarihi kur tablosuna geç
        }

        // 3. Sipariş gününün gerçek tarihi kurunu sağlayıcıdan al
        return HistoricalExchangeRateProvider.GetRateForDate(date.Date, 36.50m);
    }
}
