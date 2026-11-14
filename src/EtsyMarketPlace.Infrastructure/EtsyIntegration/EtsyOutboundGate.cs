using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

/// <summary>
/// Tüm Etsy API istemcileri (EtsyApiClient, EtsyLogisticsApiClient, EtsyListingApiClient) için
/// ortak giden eşzamanlılık kapısı, mağaza bazlı token yenileme kilidi ve 429 cooldown yönetimi.
/// </summary>
internal static class EtsyOutboundGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _shopRefreshLocks = new();
    private static readonly ConcurrentDictionary<string, long> _shopCooldownUntilTicks = new();
    private static readonly ConcurrentDictionary<string, EtsyRateLimitSnapshot> _rateLimitSnapshots = new();
    private static readonly SemaphoreSlim _globalConcurrencyGate = new(3, 3);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxCooldownWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DailyLimitCooldownWindow = TimeSpan.FromSeconds(60);

    public static EtsyRateLimitSnapshot? GetSnapshot(string shopId) =>
        string.IsNullOrWhiteSpace(shopId) ? null : (_rateLimitSnapshots.TryGetValue(shopId, out var snap) ? snap : null);

    public static async Task<EtsyOAuthToken> GetValidTokenAsync(
        string shopId,
        IEtsyTokenStore tokenStore,
        IEtsyOAuthService oauthService,
        CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetAsync(shopId, cancellationToken)
            ?? throw new InvalidOperationException($"Mağaza #{shopId} için aktif OAuth token bulunamadı.");

        if (token.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return token;

        var shopLock = _shopRefreshLocks.GetOrAdd(shopId, _ => new SemaphoreSlim(1, 1));
        await shopLock.WaitAsync(cancellationToken);
        try
        {
            token = await tokenStore.GetAsync(shopId, cancellationToken)
                ?? throw new InvalidOperationException($"Mağaza #{shopId} için aktif OAuth token bulunamadı.");

            if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                token = await oauthService.RefreshTokenAsync(shopId, token.RefreshToken, cancellationToken);
                await tokenStore.SaveAsync(shopId, token, cancellationToken);
            }

            return token;
        }
        finally
        {
            shopLock.Release();
        }
    }

    public static async Task<HttpResponseMessage> SendAsync(
        string shopId,
        Func<CancellationToken, Task<HttpResponseMessage>> sendOnce,
        CancellationToken cancellationToken)
    {
        if (_shopCooldownUntilTicks.TryGetValue(shopId, out var cooldownTicks))
        {
            var remaining = new DateTimeOffset(cooldownTicks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            if (remaining > MaxRetryDelay)
            {
                return CreateRateLimitedResponse(remaining, GetSnapshot(shopId)?.IsDailyLimitExceeded == true);
            }

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }

        var response = await SendWithGateAsync(sendOnce, cancellationToken);
        var snapshot = await CaptureRateLimitTelemetryAsync(shopId, response, cancellationToken);

        if ((int)response.StatusCode == 429)
        {
            if (snapshot.IsDailyLimitExceeded)
            {
                _shopCooldownUntilTicks[shopId] = DateTimeOffset.UtcNow.Add(DailyLimitCooldownWindow).UtcTicks;
                return response;
            }

            var retryAfter = response.Headers.RetryAfter?.Delta;
            if (retryAfter.HasValue && retryAfter.Value > TimeSpan.FromSeconds(5))
            {
                var cooldown = retryAfter.Value < MaxCooldownWindow ? retryAfter.Value : MaxCooldownWindow;
                _shopCooldownUntilTicks[shopId] = DateTimeOffset.UtcNow.Add(cooldown).UtcTicks;
                return response;
            }

            var delay = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero
                ? (retryAfter.Value <= MaxRetryDelay ? retryAfter.Value : MaxRetryDelay)
                : TimeSpan.FromSeconds(1);

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
            response = await SendWithGateAsync(sendOnce, cancellationToken);
            snapshot = await CaptureRateLimitTelemetryAsync(shopId, response, cancellationToken);

            if ((int)response.StatusCode == 429)
            {
                var cooldown = snapshot.IsDailyLimitExceeded ? DailyLimitCooldownWindow : TimeSpan.FromSeconds(5);
                _shopCooldownUntilTicks[shopId] = DateTimeOffset.UtcNow.Add(cooldown).UtcTicks;
            }
        }

        return response;
    }

    private static async Task<EtsyRateLimitSnapshot> CaptureRateLimitTelemetryAsync(
        string shopId,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var dailyLimit = TryParseIntHeader(response, "x-limit-per-day");
        var remainingToday = TryParseIntHeader(response, "x-remaining-today");
        var perSecondLimit = TryParseIntHeader(response, "x-limit-per-second");
        var remainingThisSecond = TryParseIntHeader(response, "x-remaining-this-second");
        long? retryAfterSeconds = response.Headers.RetryAfter?.Delta is { } delta
            ? (long)Math.Ceiling(delta.TotalSeconds)
            : TryParseLongHeader(response, "retry-after");

        string? errorMsg = null;
        var isDailyExceeded = remainingToday == 0;

        if ((int)response.StatusCode == 429)
        {
            try
            {
                var body = response.Content is not null
                    ? await response.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    errorMsg = body.Length > 240 ? body[..240] : body;
                    if (body.Contains("Exceeded daily rate limit", StringComparison.OrdinalIgnoreCase))
                    {
                        isDailyExceeded = true;
                        remainingToday ??= 0;
                    }
                }
            }
            catch
            {
                // Telemetri okumasi ana akisi bozmaz
            }
        }

        _rateLimitSnapshots.TryGetValue(shopId, out var prev);

        var snapshot = new EtsyRateLimitSnapshot(
            ShopId: shopId,
            DailyLimit: dailyLimit ?? prev?.DailyLimit,
            RemainingToday: remainingToday ?? (isDailyExceeded ? 0 : prev?.RemainingToday),
            PerSecondLimit: perSecondLimit ?? prev?.PerSecondLimit,
            RemainingThisSecond: remainingThisSecond ?? prev?.RemainingThisSecond,
            RetryAfterSeconds: retryAfterSeconds,
            IsDailyLimitExceeded: isDailyExceeded,
            LastErrorMessage: errorMsg,
            UpdatedAt: DateTimeOffset.UtcNow);

        _rateLimitSnapshots[shopId] = snapshot;
        return snapshot;
    }

    private static int? TryParseIntHeader(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return null;
    }

    private static long? TryParseLongHeader(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out var values))
        {
            var raw = values.FirstOrDefault();
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return null;
    }

    private static async Task<HttpResponseMessage> SendWithGateAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sendOnce,
        CancellationToken cancellationToken)
    {
        await _globalConcurrencyGate.WaitAsync(cancellationToken);
        try
        {
            return await sendOnce(cancellationToken);
        }
        finally
        {
            _globalConcurrencyGate.Release();
        }
    }

    private static HttpResponseMessage CreateRateLimitedResponse(TimeSpan retryAfter, bool isDailyExceeded)
    {
        var message = isDailyExceeded
            ? "{\"error\":\"Exceeded daily rate limit\"}"
            : "Etsy API 429 cooldown aktif; yedek veri kullanılıyor.";
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(message)
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(Math.Ceiling(retryAfter.TotalSeconds)));
        return response;
    }
}

public sealed class EtsyRateLimitStatusProvider : IEtsyRateLimitStatusProvider
{
    public EtsyRateLimitSnapshot? GetSnapshot(string shopId) => EtsyOutboundGate.GetSnapshot(shopId);
}
