using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy")]
public class EtsyOAuthController : BaseApiController
{
    private static readonly ConcurrentDictionary<string, PkceSession> PendingPkceSessions = new();

    private readonly IConfiguration _config;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IEtsyOAuthService _oauthService;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IHttpClientFactory _httpClientFactory;

    public EtsyOAuthController(
        IConfiguration config,
        IEtsyTokenStore tokenStore,
        IEtsyOAuthService oauthService,
        IShopSettingsRepository settingsRepo,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _tokenStore = tokenStore;
        _oauthService = oauthService;
        _settingsRepo = settingsRepo;
        _httpClientFactory = httpClientFactory;
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
        var resolvedShopId = ResolveShopId(shopId, _config);
        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var targetRedirectUri = !string.IsNullOrWhiteSpace(redirectUri)
            ? redirectUri.Trim()
            : (!string.IsNullOrWhiteSpace(raw.RedirectUri) ? raw.RedirectUri : (_config["Etsy:RedirectUri"] ?? "http://localhost:4200/settings/etsy-api"));

        if (string.IsNullOrWhiteSpace(keystring))
            return BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı. Lütfen önce API ayarlarından Keystring kaydedin." });

        var verifierBytes = RandomNumberGenerator.GetBytes(64);
        var codeVerifier = EtsyOAuthService.Base64Url(verifierBytes);
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var codeChallenge = EtsyOAuthService.Base64Url(challengeBytes);
        var state = EtsyOAuthService.Base64Url(RandomNumberGenerator.GetBytes(32));

        PendingPkceSessions[state] = new PkceSession(codeVerifier, resolvedShopId, targetRedirectUri, DateTimeOffset.UtcNow);

        var expireThreshold = DateTimeOffset.UtcNow.AddMinutes(-30);
        foreach (var kvp in PendingPkceSessions)
        {
            if (kvp.Value.CreatedAt < expireThreshold)
                PendingPkceSessions.TryRemove(kvp.Key, out _);
        }

        var scopes = "listings_r listings_w shops_r transactions_r billing_r";
        var authBase = _config["Etsy:AuthorizationBaseUrl"] ?? "https://www.etsy.com/oauth/connect";
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = keystring.Trim(),
            ["redirect_uri"] = targetRedirectUri.Trim(),
            ["scope"] = scopes,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var url = authBase + "?" + string.Join('&', query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return Ok(new
        {
            url,
            state,
            redirectUri = targetRedirectUri,
            shopId = resolvedShopId
        });
    }

    [HttpPost("oauth/exchange-code")]
    [EndpointSummary("Etsy OAuth Yetki Kodunu Token'a Dönüştür (Code Exchange)")]
    public async Task<IActionResult> ExchangeEtsyOAuthCode([FromBody] ExchangeCodeApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId) || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { success = false, message = "ShopId ve yetki kodu (code) gereklidir." });

        var shopId = request.ShopId.Trim();
        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(shopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        if (string.IsNullOrWhiteSpace(keystring))
            return BadRequest(new { success = false, message = "Etsy Keystring (Client ID) bulunamadı. Lütfen önce API ayarlarından Keystring kaydedin." });

        string? codeVerifier = request.CodeVerifier;
        string targetRedirectUri = !string.IsNullOrWhiteSpace(request.RedirectUri) ? request.RedirectUri.Trim() : (!string.IsNullOrWhiteSpace(raw.RedirectUri) ? raw.RedirectUri : (_config["Etsy:RedirectUri"] ?? "http://localhost:4200/settings/etsy-api"));

        if (!string.IsNullOrWhiteSpace(request.State) && PendingPkceSessions.TryRemove(request.State, out var session))
        {
            codeVerifier ??= session.CodeVerifier;
            if (string.IsNullOrWhiteSpace(request.RedirectUri))
                targetRedirectUri = session.RedirectUri;
        }

        if (string.IsNullOrWhiteSpace(codeVerifier))
            return BadRequest(new { success = false, message = "PKCE code_verifier bulunamadı veya oturum süresi doldu. Lütfen 'Etsy ile Yetkilendir' butonunu tekrar tıklayın." });

        var tokenUrl = _config["Etsy:TokenUrl"] ?? "https://api.etsy.com/v3/public/oauth/token";
        var client = _httpClientFactory.CreateClient();

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = keystring.Trim(),
                ["redirect_uri"] = targetRedirectUri.Trim(),
                ["code"] = request.Code.Trim(),
                ["code_verifier"] = codeVerifier.Trim()
            })
        };

        tokenRequest.Headers.Add("x-api-key", !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim());

        try
        {
            using var response = await client.SendAsync(tokenRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Etsy yetkilendirme takası başarısız (HTTP {(int)response.StatusCode}): {body}"
                });
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var accessToken = root.GetProperty("access_token").GetString() ?? "";
            var refreshToken = root.GetProperty("refresh_token").GetString() ?? "";
            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            var tokenType = root.TryGetProperty("token_type", out var tt) ? (tt.GetString() ?? "Bearer") : "Bearer";

            var newToken = new EtsyOAuthToken(accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn), tokenType);
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
