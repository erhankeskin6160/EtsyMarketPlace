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

        var token = await EtsyOutboundGate.GetValidTokenAsync(shopId, tokenStore, oauthService, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);

        var response = await EtsyOutboundGate.SendAsync(
            shopId,
            ct => base.SendAsync(request, ct),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            response.Headers.Add("X-Etsy-Token-Expired", "true");

        return response;
    }
}