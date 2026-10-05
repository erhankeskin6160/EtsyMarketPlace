using System.Net;
using System.Net.Http.Headers;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyAccessTokenHandler(IEtsyTokenStore tokenStore, IEtsyOAuthService oauthService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues("X-Etsy-Shop-Id", out var values))
            throw new InvalidOperationException("Etsy isteği için X-Etsy-Shop-Id başlığı gereklidir.");

        var shopId = values.Single();
        request.Headers.Remove("X-Etsy-Shop-Id");
        var token = await tokenStore.GetAsync(shopId, cancellationToken) ?? throw new InvalidOperationException("Mağaza için Etsy OAuth token bulunamadı.");
        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            token = await oauthService.RefreshTokenAsync(shopId, token.RefreshToken, cancellationToken);
            await tokenStore.SaveAsync(shopId, token, cancellationToken);
        }
        request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode == 429)
        {
            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
            response = await base.SendAsync(request, cancellationToken);
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            response.Headers.Add("X-Etsy-Token-Expired", "true");
        return response;
    }
}