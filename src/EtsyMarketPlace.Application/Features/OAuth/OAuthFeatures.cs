namespace EtsyMarketPlace.Application.Features.OAuth;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using MediatR;

// 1. Import Token
public sealed record ImportEtsyTokenCommand(
    string ShopId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string TokenType = "Bearer") : IRequest<ServiceResponse>;

public sealed class ImportEtsyTokenHandler(IEtsyTokenStore tokenStore)
    : IRequestHandler<ImportEtsyTokenCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ImportEtsyTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId) ||
            string.IsNullOrWhiteSpace(request.AccessToken) ||
            string.IsNullOrWhiteSpace(request.RefreshToken) ||
            request.AccessTokenExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ServiceResponse.CreateError("ShopId, geçerli access token, refresh token ve gelecekteki token son kullanma zamanı zorunludur.", HttpStatusCode.BadRequest);
        }

        var shopId = request.ShopId.Trim();
        var tokenType = string.IsNullOrWhiteSpace(request.TokenType) ? "Bearer" : request.TokenType.Trim();

        await tokenStore.SaveAsync(
            shopId,
            new EtsyOAuthToken(
                request.AccessToken.Trim(),
                request.RefreshToken.Trim(),
                request.AccessTokenExpiresAt,
                tokenType),
            cancellationToken);

        var savedToken = await tokenStore.GetAsync(shopId, cancellationToken);
        if (savedToken is null)
            return ServiceResponse.CreateError("Token kaydedilemedi.", HttpStatusCode.InternalServerError);

        return ServiceResponse.CreateSuccess("Etsy token başarıyla kaydedildi.", new
        {
            saved = true,
            shopId,
            expiresAt = savedToken.AccessTokenExpiresAt,
            isExpired = savedToken.AccessTokenExpiresAt <= DateTimeOffset.UtcNow,
            tokenType = savedToken.TokenType
        });
    }
}

// 2. Token Status
public sealed record GetEtsyTokenStatusQuery(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetEtsyTokenStatusHandler(
    IEtsyTokenStore tokenStore,
    IEtsyOAuthService oauthService)
    : IRequestHandler<GetEtsyTokenStatusQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetEtsyTokenStatusQuery request, CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetAsync(request.ShopId, cancellationToken);
        if (token is null)
            return ServiceResponse.CreateError("Token bulunamadı.", HttpStatusCode.NotFound);

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow && !string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            try
            {
                var refreshed = await oauthService.RefreshTokenAsync(request.ShopId, token.RefreshToken, cancellationToken);
                await tokenStore.SaveAsync(request.ShopId, refreshed, cancellationToken);
                token = refreshed;
            }
            catch
            {
                // Refresh token da geçersizleşmişse isExpired true kalır
            }
        }

        return ServiceResponse.CreateSuccess("Token durumu getirildi.", new
        {
            exists = true,
            shopId = request.ShopId,
            expiresAt = token.AccessTokenExpiresAt,
            isExpired = token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow,
            tokenType = token.TokenType
        });
    }
}

// 3. Refresh Token
public sealed record RefreshEtsyTokenCommand(string ShopId) : IRequest<ServiceResponse>;

public sealed class RefreshEtsyTokenHandler(
    IEtsyTokenStore tokenStore,
    IEtsyOAuthService oauthService)
    : IRequestHandler<RefreshEtsyTokenCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(RefreshEtsyTokenCommand request, CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetAsync(request.ShopId, cancellationToken);
        if (token is null)
            return ServiceResponse.CreateError("Bu mağaza için kayıtlı token bulunamadı.", HttpStatusCode.NotFound);

        try
        {
            var refreshed = await oauthService.RefreshTokenAsync(request.ShopId, token.RefreshToken, cancellationToken);
            await tokenStore.SaveAsync(request.ShopId, refreshed, cancellationToken);
            return ServiceResponse.CreateSuccess("Etsy OAuth v3 token başarıyla yenilendi.", new
            {
                success = true,
                shopId = request.ShopId,
                expiresAt = refreshed.AccessTokenExpiresAt,
                isExpired = false,
                message = "Etsy OAuth v3 token başarıyla yenilendi."
            });
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError("Token yenilenemedi: " + ex.Message, HttpStatusCode.BadRequest);
        }
    }
}

// 4. Connect Url
public sealed record GetEtsyOAuthConnectUrlQuery(string ShopId, string? RedirectUri) : IRequest<ServiceResponse>;

public sealed class GetEtsyOAuthConnectUrlHandler(IEtsyOAuthService oauthService)
    : IRequestHandler<GetEtsyOAuthConnectUrlQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetEtsyOAuthConnectUrlQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await oauthService.CreatePkceConnectUrlAsync(request.ShopId, request.RedirectUri, cancellationToken);
            return ServiceResponse.CreateSuccess("Etsy yetkilendirme bağlantısı üretildi.", result);
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError(ex.Message, HttpStatusCode.BadRequest);
        }
    }
}

// 5. Exchange Code
public sealed record ExchangeEtsyOAuthCodeCommand(
    string ShopId,
    string Code,
    string? State,
    string? CodeVerifier,
    string? RedirectUri) : IRequest<ServiceResponse>;

public sealed class ExchangeEtsyOAuthCodeHandler(
    IEtsyTokenStore tokenStore,
    IEtsyOAuthService oauthService)
    : IRequestHandler<ExchangeEtsyOAuthCodeCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ExchangeEtsyOAuthCodeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.Code))
            return ServiceResponse.CreateError("ShopId ve yetki kodu (code) gereklidir.", HttpStatusCode.BadRequest);

        try
        {
            var shopId = request.ShopId.Trim();
            var newToken = await oauthService.ExchangeCodeWithPkceAsync(
                shopId,
                request.Code.Trim(),
                request.State,
                request.CodeVerifier,
                request.RedirectUri,
                cancellationToken);

            await tokenStore.SaveAsync(shopId, newToken, cancellationToken);

            return ServiceResponse.CreateSuccess("Etsy OAuth v3 bağlantısı ve yetkilendirmesi başarıyla tamamlandı!", new
            {
                success = true,
                shopId,
                expiresAt = newToken.AccessTokenExpiresAt,
                isExpired = false,
                message = "Etsy OAuth v3 bağlantısı ve yetkilendirmesi başarıyla tamamlandı!"
            });
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError("Etsy token takas hatası: " + ex.Message, HttpStatusCode.BadRequest);
        }
    }
}
