namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Shipping;
using Microsoft.Extensions.Configuration;

public sealed class EtsyLogisticsApiClient : IEtsyLogisticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IEtsyOAuthService _oauthService;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IConfiguration _config;

    public EtsyLogisticsApiClient(
        HttpClient httpClient,
        IEtsyTokenStore tokenStore,
        IEtsyOAuthService oauthService,
        IShopSettingsRepository settingsRepo,
        IConfiguration config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    private async Task<(string AccessToken, string ApiKeyHeader)> PrepareAuthAsync(string shopId, CancellationToken ct)
    {
        var token = await _tokenStore.GetAsync(shopId, ct)
            ?? throw new InvalidOperationException($"Mağaza #{shopId} için aktif OAuth token bulunamadı.");

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            token = await _oauthService.RefreshTokenAsync(shopId, token.RefreshToken, ct);
            await _tokenStore.SaveAsync(shopId, token, ct);
        }

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(shopId, ct);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);

        if (string.IsNullOrWhiteSpace(keystring))
            throw new InvalidOperationException("Etsy Keystring (Client ID) bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin.");

        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();
        return (token.AccessToken, apiKeyHeader);
    }

    public async Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/shipping-profiles";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Kargo profilleri Etsy'den alınamadı (HTTP {(int)res.StatusCode}): {body}");
        }
        return body;
    }

    public async Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/readiness-state-definitions";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _httpClient.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/listings/active?limit=100";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _httpClient.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken ct)
    {
        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(shopId, ct);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var url = "https://api.etsy.com/v3/application/seller-taxonomy/nodes";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Etsy taxonomy nodes API çağrısı başarısız (HTTP {(int)res.StatusCode}): {body}");
        }
        return body;
    }
}
