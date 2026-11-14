using System.Collections.Concurrent;
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
    private static readonly SemaphoreSlim _globalConcurrencyGate = new(3, 3);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxCooldownWindow = TimeSpan.FromSeconds(15);

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
                return CreateRateLimitedResponse(remaining);
            }

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }

        var response = await SendWithGateAsync(sendOnce, cancellationToken);
        if ((int)response.StatusCode == 429)
        {
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
            if ((int)response.StatusCode == 429)
            {
                _shopCooldownUntilTicks[shopId] = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(5)).UtcTicks;
            }
        }

        return response;
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

    private static HttpResponseMessage CreateRateLimitedResponse(TimeSpan retryAfter)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("Etsy API 429 cooldown aktif; yedek veri kullanılıyor.")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(Math.Ceiling(retryAfter.TotalSeconds)));
        return response;
    }
}
