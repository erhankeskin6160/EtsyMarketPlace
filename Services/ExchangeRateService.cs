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
        var fallback = _cachedLiveRate ?? HistoricalExchangeRateProvider.GetRateForDate(DateTime.Today, 48.25m);
        return fallback;
    }

    /// <summary>
    /// Sipariş için kilitli kuru döndürür. Yoksa Etsy API / TCMB / Tarihi API'den çekip DB'ye kilitler.
    /// Böylece geçmiş siparişlerin TL kurları gelecekte ASLA değişmez.
    /// </summary>
    public async Task<decimal> GetOrLockRateForOrderAsync(
        long receiptId,
        DateTime orderDate,
        decimal? etsyDirectRate,
        decimal defaultExchangeRate,
        CancellationToken ct = default)
    {
        // 1. SQLite'da bu sipariş için daha önce kilitlenmiş bir kur var mı?
        var lockedRate = await _repository.GetLockedRateForOrderAsync(receiptId, ct);
        if (lockedRate.HasValue && lockedRate.Value >= 30m)
        {
            return lockedRate.Value;
        }

        // 2. Etsy API'den doğrudan gelen TL/USD hakediş kuru var mı? (En yüksek öncelik)
        if (etsyDirectRate.HasValue && etsyDirectRate.Value >= 30m)
        {
            decimal rate = Math.Round(etsyDirectRate.Value, 2);
            await _repository.LockRateForOrderAsync(receiptId, orderDate, rate, "ETSY_DIRECT", ct);
            await _repository.SaveDailyHistoricalRateAsync(orderDate, rate, "ETSY_PAYMENT", ct);
            return rate;
        }

        // 3. O güne ait SQLite'da kayıtlı resmi tarihi kur var mı?
        var dailyRate = await _repository.GetDailyHistoricalRateAsync(orderDate, ct);
        if (dailyRate.HasValue && dailyRate.Value >= 30m)
        {
            await _repository.LockRateForOrderAsync(receiptId, orderDate, dailyRate.Value, "DB_HISTORICAL", ct);
            return dailyRate.Value;
        }

        // 4. API üzerinden o günün tarihi kurunu çek
        decimal fetchedRate = await GetHistoricalRateAsync(orderDate, ct);
        if (fetchedRate < 30m)
        {
            fetchedRate = defaultExchangeRate > 30m ? defaultExchangeRate : HistoricalExchangeRateProvider.GetRateForDate(orderDate, 48.25m);
        }

        fetchedRate = Math.Round(fetchedRate, 2);
        await _repository.LockRateForOrderAsync(receiptId, orderDate, fetchedRate, "API_HISTORICAL", ct);
        await _repository.SaveDailyHistoricalRateAsync(orderDate, fetchedRate, "API_HISTORICAL", ct);
        return fetchedRate;
    }

    public async Task<decimal> GetHistoricalRateAsync(DateTime date, CancellationToken ct = default)
    {
        // En geç bugünü kullanabiliriz, yarına ait kur olmaz
        if (date.Date > DateTime.UtcNow.Date) date = DateTime.UtcNow.Date;

        // 1. Önce veritabanında geçerli bir kur var mı bakıyoruz (>= 35 TL)
        var cachedRate = await _repository.GetDailyHistoricalRateAsync(date.Date, ct) 
                         ?? await _repository.GetRateAsync(date.Date, ct);
        if (cachedRate.HasValue && cachedRate.Value >= 35m)
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
                    if (rate >= 35m)
                    {
                        rate = Math.Round(rate, 2);
                        await _repository.SaveDailyHistoricalRateAsync(date.Date, rate, "FRANKFURTER_API", ct);
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
        var fallback = HistoricalExchangeRateProvider.GetRateForDate(date.Date, 48.25m);
        await _repository.SaveDailyHistoricalRateAsync(date.Date, fallback, "HISTORICAL_PROVIDER", ct);
        return fallback;
    }

    /// <summary>
    /// Verilen tarihlerin tamamını öncelikle yerel SQLite veritabanından tek sorguda çeker.
    /// Yalnızca veritabanında kaydı olmayan eksik tarihleri API'den çekip DB'ye kaydeder.
    /// Böylece dış API'ler gereksiz yere yorulmaz ve 0 ms gecikmeyle sonuç üretilir.
    /// </summary>
    public async Task<Dictionary<DateTime, decimal>> HydrateAndResolveRatesAsync(
        IEnumerable<DateTime> dates,
        decimal fallbackRate,
        CancellationToken ct = default)
    {
        var distinctDates = dates.Select(d => d.Date).Distinct().ToList();
        var result = await _repository.GetRatesForDatesAsync(distinctDates, ct);

        // Eksik kalan günleri tespit et (DB'de henüz kaydı olmayanlar)
        var missingDates = distinctDates.Where(d => !result.ContainsKey(d) || result[d] < 35m).ToList();

        if (missingDates.Count > 0)
        {
            var newlyFetched = new Dictionary<DateTime, decimal>();
            var today = DateTime.UtcNow.Date;

            foreach (var mDate in missingDates)
            {
                if (ct.IsCancellationRequested) break;

                // Gelecek tarihler için bugünün kuru geçerlidir
                var queryDate = mDate > today ? today : mDate;

                decimal rate;
                if (queryDate == today)
                {
                    rate = await GetLiveUsdTryRateAsync(ct);
                }
                else
                {
                    rate = await GetHistoricalRateAsync(queryDate, ct);
                }

                if (rate < 35m)
                {
                    rate = fallbackRate > 35m ? fallbackRate : HistoricalExchangeRateProvider.GetRateForDate(mDate, 48.25m);
                }

                newlyFetched[mDate] = Math.Round(rate, 2);
                result[mDate] = newlyFetched[mDate];
            }

            if (newlyFetched.Count > 0)
            {
                await _repository.BulkSaveDailyRatesAsync(newlyFetched, "BULK_GAP_FILL", ct);
            }
        }

        // Kalan her tarih için garanti kur sağla
        foreach (var d in distinctDates)
        {
            if (!result.ContainsKey(d) || result[d] < 35m)
            {
                result[d] = fallbackRate > 35m ? fallbackRate : HistoricalExchangeRateProvider.GetRateForDate(d, 48.25m);
            }
        }

        return result;
    }
}
