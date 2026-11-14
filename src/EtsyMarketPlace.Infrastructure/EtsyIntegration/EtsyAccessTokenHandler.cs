using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyAccessTokenHandler(IEtsyTokenStore tokenStore, IEtsyOAuthService oauthService) : DelegatingHandler
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _shopRefreshLocks = new();
    private static readonly ConcurrentDictionary<string, long> _shopCooldownUntilTicks = new();
    private static readonly SemaphoreSlim _globalConcurrencyGate = new(3, 3);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxCooldownWindow = TimeSpan.FromSeconds(15);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues("X-Etsy-Shop-Id", out var values))
            throw new InvalidOperationException("Etsy isteği için X-Etsy-Shop-Id başlığı gereklidir.");

        var shopId = values.Single();
        request.Headers.Remove("X-Etsy-Shop-Id");

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

        var token = await GetValidTokenAsync(shopId, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);

        var response = await SendWithGateAsync(request, cancellationToken);
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
            response = await SendWithGateAsync(request, cancellationToken);
            if ((int)response.StatusCode == 429)
            {
                _shopCooldownUntilTicks[shopId] = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(5)).UtcTicks;
            }
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            response.Headers.Add("X-Etsy-Token-Expired", "true");

        return response;
    }

    private async Task<EtsyOAuthToken> GetValidTokenAsync(string shopId, CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetAsync(shopId, cancellationToken)
            ?? throw new InvalidOperationException("Mağaza için Etsy OAuth token bulunamadı.");

        if (token.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return token;

        var shopLock = _shopRefreshLocks.GetOrAdd(shopId, _ => new SemaphoreSlim(1, 1));
        await shopLock.WaitAsync(cancellationToken);
        try
        {
            token = await tokenStore.GetAsync(shopId, cancellationToken)
                ?? throw new InvalidOperationException("Mağaza için Etsy OAuth token bulunamadı.");

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

    private async Task<HttpResponseMessage> SendWithGateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _globalConcurrencyGate.WaitAsync(cancellationToken);
        try
        {
            return await base.SendAsync(request, cancellationToken);
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