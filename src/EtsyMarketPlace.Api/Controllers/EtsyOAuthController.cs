using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy")]
public class EtsyOAuthController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IEtsyOAuthService _oauthService;

    public EtsyOAuthController(
        IConfiguration config,
        IEtsyTokenStore tokenStore,
        IEtsyOAuthService oauthService)
    {
        _config = config;
        _tokenStore = tokenStore;
        _oauthService = oauthService;
    }

    [HttpPost("token")]
    [EndpointSummary("Etsy OAuth Token Aktarımı")]
    public async Task<IActionResult> ImportEtsyToken([FromBody] EtsyTokenImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId) ||
            string.IsNullOrWhiteSpace(request.AccessToken) ||
            string.IsNullOrWhiteSpace(request.RefreshToken) ||
            request.AccessTokenExpiresAt <= DateTimeOffset.UtcNow)
        {
            return BadRequest(new { error = "ShopId, geçerli access token, refresh token ve gelecekteki token son kullanma zamanı zorunludur." });
        }

        var tokenType = string.IsNullOrWhiteSpace(request.TokenType) ? "Bearer" : request.TokenType.Trim();
        var shopId = request.ShopId.Trim();
        await _tokenStore.SaveAsync(
            shopId,
            new EtsyOAuthToken(
                request.AccessToken.Trim(),
                request.RefreshToken.Trim(),
                request.AccessTokenExpiresAt,
                tokenType),
            cancellationToken);

        var savedToken = await _tokenStore.GetAsync(shopId, cancellationToken);
        return savedToken is null
            ? Problem("Token kaydedilemedi.", statusCode: StatusCodes.Status500InternalServerError)
            : Ok(new
            {
                saved = true,
                shopId,
                expiresAt = savedToken.AccessTokenExpiresAt,
                isExpired = savedToken.AccessTokenExpiresAt <= DateTimeOffset.UtcNow,
                tokenType = savedToken.TokenType
            });
    }

    [HttpGet("token/status")]
    [EndpointSummary("Etsy Token Durum ve Geçerlilik Kontrolü")]
    public async Task<IActionResult> GetEtsyTokenStatus([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token is null)
            return NotFound(new { exists = false, shopId = resolvedShopId });

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow && !string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            try
            {
                var refreshed = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
                await _tokenStore.SaveAsync(resolvedShopId, refreshed, cancellationToken);
                token = refreshed;
            }
            catch
            {
                // Refresh token da geçersizleşmişse isExpired: true dönecek
            }
        }

        return Ok(new
        {
            exists = true,
            shopId = resolvedShopId,
            expiresAt = token.AccessTokenExpiresAt,
            isExpired = token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow,
            tokenType = token.TokenType
        });
    }

    [HttpPost("token/refresh")]
    [EndpointSummary("Etsy Token Yenileme (Refresh Token)")]
    public async Task<IActionResult> RefreshEtsyToken([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token is null)
            return NotFound(new { success = false, message = "Bu mağaza için kayıtlı token bulunamadı." });

        try
        {
            var refreshed = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
            await _tokenStore.SaveAsync(resolvedShopId, refreshed, cancellationToken);
            return Ok(new
            {
                success = true,
                shopId = resolvedShopId,
                expiresAt = refreshed.AccessTokenExpiresAt,
                isExpired = false,
                message = "Etsy OAuth v3 token başarıyla yenilendi."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                shopId = resolvedShopId,
                message = "Token yenilenemedi: " + ex.Message,
                needsReauth = true
            });
        }
    }

    [HttpGet("oauth/connect-url")]
    [EndpointSummary("Etsy OAuth v3 PKCE Yetkilendirme Bağlantısı Üret")]
    public async Task<IActionResult> GetEtsyOAuthConnectUrl([FromQuery] string shopId = "53236321", [FromQuery] string? redirectUri = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedShopId = ResolveShopId(shopId, _config);
            var result = await _oauthService.CreatePkceConnectUrlAsync(resolvedShopId, redirectUri, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("oauth/exchange-code")]
    [EndpointSummary("Etsy OAuth Yetki Kodunu Token'a Dönüştür (Code Exchange)")]
    public async Task<IActionResult> ExchangeEtsyOAuthCode([FromBody] ExchangeCodeApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { success = false, message = "ShopId ve yetki kodu (code) gereklidir." });

        try
        {
            var shopId = request.ShopId.Trim();
            var newToken = await _oauthService.ExchangeCodeWithPkceAsync(
                shopId,
                request.Code.Trim(),
                request.State,
                request.CodeVerifier,
                request.RedirectUri,
                cancellationToken);

            await _tokenStore.SaveAsync(shopId, newToken, cancellationToken);

            return Ok(new
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
            return BadRequest(new { success = false, message = "Etsy token takas hatası: " + ex.Message });
        }
    }
}
