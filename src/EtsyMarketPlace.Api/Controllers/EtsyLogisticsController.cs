using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy")]
public class EtsyLogisticsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEtsyOAuthService _oauthService;

    public EtsyLogisticsController(
        IConfiguration config,
        IEtsyTokenStore tokenStore,
        IShopSettingsRepository settingsRepo,
        IHttpClientFactory httpClientFactory,
        IEtsyOAuthService oauthService)
    {
        _config = config;
        _tokenStore = tokenStore;
        _settingsRepo = settingsRepo;
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
    }

    [HttpGet("shipping-profiles")]
    [EndpointSummary("Mağazanın Etsy Kargo Profillerini Canlı Getir")]
    public async Task<IActionResult> GetEtsyShippingProfiles([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token is null)
            return NotFound(new { error = "Bu mağaza için kayıtlı OAuth token bulunamadı." });

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            try
            {
                token = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
                await _tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Etsy token yenilenemedi: " + ex.Message });
            }
        }

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var client = _httpClientFactory.CreateClient();
        var profUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/shipping-profiles";
        using var profReq = new HttpRequestMessage(HttpMethod.Get, profUrl);
        profReq.Headers.Add("x-api-key", apiKeyHeader);
        profReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var profRes = await client.SendAsync(profReq, cancellationToken);
        var profBody = await profRes.Content.ReadAsStringAsync(cancellationToken);
        if (!profRes.IsSuccessStatusCode)
        {
            return BadRequest(new { error = $"Kargo profilleri Etsy'den alınamadı (HTTP {(int)profRes.StatusCode}): {profBody}" });
        }

        return Content(profBody, "application/json");
    }

    [HttpGet("readiness-states")]
    [EndpointSummary("Mağazanın Etsy Hazırlık Durumlarını (readiness-state-definitions) Canlı Getir")]
    public async Task<IActionResult> GetEtsyReadinessStates([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token is null)
            return NotFound(new { error = "Bu mağaza için kayıtlı OAuth token bulunamadı." });

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            try
            {
                token = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
                await _tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Etsy token yenilenemedi: " + ex.Message });
            }
        }

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var client = _httpClientFactory.CreateClient();
        var list = new List<object>();

        // 1. Try official readiness-state-definitions endpoint
        try
        {
            var defUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/readiness-state-definitions";
            using var defReq = new HttpRequestMessage(HttpMethod.Get, defUrl);
            defReq.Headers.Add("x-api-key", apiKeyHeader);
            defReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

            using var defRes = await client.SendAsync(defReq, cancellationToken);
            if (defRes.IsSuccessStatusCode)
            {
                var defBody = await defRes.Content.ReadAsStringAsync(cancellationToken);
                using var defDoc = JsonDocument.Parse(defBody);
                if (defDoc.RootElement.TryGetProperty("results", out var rArray) && rArray.GetArrayLength() > 0)
                {
                    foreach (var item in rArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("readiness_state_id", out var idProp))
                        {
                            var id = idProp.GetInt64();
                            var state = item.TryGetProperty("readiness_state", out var sProp) ? sProp.GetString() : null;
                            var minTime = item.TryGetProperty("min_processing_time", out var minE) && minE.TryGetInt32(out var minVal) ? minVal : 0;
                            var maxTime = item.TryGetProperty("max_processing_time", out var maxE) && maxE.TryGetInt32(out var maxVal) ? maxVal : 0;
                            var unit = item.TryGetProperty("processing_time_unit", out var unitE) ? unitE.GetString() ?? "iş günü" : "iş günü";

                            string title;
                            if (!string.IsNullOrWhiteSpace(state))
                            {
                                var stateTr = state.Equals("made_to_order", StringComparison.OrdinalIgnoreCase) ? "Siparişe Özel (Made to order)" : "Hazır Ürün (Ready to ship)";
                                title = (minTime > 0 || maxTime > 0) ? $"{stateTr} ({minTime}-{maxTime} {unit})" : stateTr;
                            }
                            else
                            {
                                title = $"Hazırlık Durumu #{id}";
                            }

                            list.Add(new
                            {
                                readiness_state_id = id,
                                title,
                                readiness_state = state,
                                min_processing_time = minTime,
                                max_processing_time = maxTime,
                                processing_time_unit = unit
                            });
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Fallback: Query active listings to find used readiness_state_ids
        if (list.Count == 0)
        {
            try
            {
                var actUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/active?limit=100";
                using var actReq = new HttpRequestMessage(HttpMethod.Get, actUrl);
                actReq.Headers.Add("x-api-key", apiKeyHeader);
                actReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                using var actRes = await client.SendAsync(actReq, cancellationToken);
                if (actRes.IsSuccessStatusCode)
                {
                    var actBody = await actRes.Content.ReadAsStringAsync(cancellationToken);
                    using var actDoc = JsonDocument.Parse(actBody);
                    if (actDoc.RootElement.TryGetProperty("results", out var aListings) && aListings.GetArrayLength() > 0)
                    {
                        var seenIds = new HashSet<long>();
                        foreach (var al in aListings.EnumerateArray())
                        {
                            if (al.TryGetProperty("readiness_state_id", out var rsProp) && rsProp.GetInt64() > 0)
                            {
                                var id = rsProp.GetInt64();
                                if (seenIds.Add(id))
                                {
                                    list.Add(new
                                    {
                                        readiness_state_id = id,
                                        title = $"Hazırlık Durumu #{id}",
                                        readiness_state = "ready_to_ship",
                                        min_processing_time = 1,
                                        max_processing_time = 3,
                                        processing_time_unit = "iş günü"
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        return Ok(new
        {
            count = list.Count,
            results = list
        });
    }
}
