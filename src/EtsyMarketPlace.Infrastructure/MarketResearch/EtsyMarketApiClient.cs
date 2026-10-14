namespace EtsyMarketPlace.Infrastructure.MarketResearch;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.MarketResearch;
using Microsoft.Extensions.Configuration;

public sealed class EtsyMarketApiClient : IEtsyMarketApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IEtsyOAuthService _oauthService;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IConfiguration _config;

    public EtsyMarketApiClient(
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

    public async Task<string> SearchActiveListingsRawJsonAsync(
        string shopId,
        string keyword,
        int limit,
        CancellationToken ct)
    {
        var (apiKeyHeader, bearerToken) = await ResolveAuthHeadersAsync(shopId, ct);

        var clampedLimit = Math.Clamp(limit, 1, 100);
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var url = $"https://api.etsy.com/v3/application/listings/active?keywords={encodedKeyword}&limit={clampedLimit}&sort_on=score&sort_order=desc&includes=Shop,Images";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKeyHeader);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var fallbackUrl = $"https://api.etsy.com/v3/application/listings/active?keywords={encodedKeyword}&limit={clampedLimit}&sort_on=score&sort_order=desc";
            using var fbReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
            fbReq.Headers.Add("x-api-key", apiKeyHeader);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                fbReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            using var fbResp = await _httpClient.SendAsync(fbReq, ct);
            if (!fbResp.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Etsy Pazar Arama hatası (HTTP {(int)response.StatusCode}): {err}");
            }
            return await fbResp.Content.ReadAsStringAsync(ct);
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    /// Etsy'nin toplu ilan ucundan ham JSON döndürür (görseller + mağaza bilgisi dahil).
    /// GET /v3/application/listings/batch?listing_ids=...&includes=Images,Shop
    /// </summary>
    public async Task<string> GetListingsBatchRawJsonAsync(
        string shopId,
        IReadOnlyList<long> listingIds,
        CancellationToken cancellationToken = default)
    {
        if (listingIds == null || listingIds.Count == 0)
        {
            throw new ArgumentException("En az bir ilan kimliği gerekir.", nameof(listingIds));
        }

        var (apiKeyHeader, bearerToken) = await ResolveAuthHeadersAsync(shopId, cancellationToken);

        var ids = string.Join(",", listingIds);
        var url = $"https://api.etsy.com/v3/application/listings/batch?listing_ids={ids}&includes=Images,Shop";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKeyHeader);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Etsy ilan toplu veri hatası (HTTP {(int)response.StatusCode}): {err}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Etsy uygulama kimlik bilgilerini çözer ve gerekirse erişim token'ını yeniler.
    /// x-api-key header değeri ile opsiyonel Bearer token döndürür.
    /// </summary>
    private async Task<(string ApiKeyHeader, string? BearerToken)> ResolveAuthHeadersAsync(string shopId, CancellationToken ct)
    {
        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(shopId, ct);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);

        if (string.IsNullOrWhiteSpace(keystring))
            throw new InvalidOperationException("Etsy API Keystring bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin.");

        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var token = await _tokenStore.GetAsync(shopId, ct);
        if (token != null && token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1) && !string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            try
            {
                token = await _oauthService.RefreshTokenAsync(shopId, token.RefreshToken, ct);
                await _tokenStore.SaveAsync(shopId, token, ct);
            }
            catch
            {
                // Canlı pazar araması için token yenilenemezse x-api-key ile genel arama sürdürülür
            }
        }

        return (apiKeyHeader, token?.AccessToken);
    }
}
