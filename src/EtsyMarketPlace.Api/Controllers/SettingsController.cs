using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ShopPerformance;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api")]
public class SettingsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IShopPerformanceHistoryRepository _perfRepo;

    public SettingsController(
        IConfiguration config,
        IShopSettingsRepository settingsRepo,
        IShopPerformanceHistoryRepository perfRepo)
    {
        _config = config;
        _settingsRepo = settingsRepo;
        _perfRepo = perfRepo;
    }

    [HttpGet("settings/ai")]
    [EndpointSummary("Merkezi Veritabanından AI Model ve API Anahtarlarını Getir")]
    public async Task<IActionResult> GetAiSettings([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var json = await _settingsRepo.GetAiSettingsJsonAsync(resolvedShopId, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Ok(new { exists = false, settingsJson = "{}" });
        }
        return Ok(new { exists = true, settingsJson = json });
    }

    [HttpPost("settings/ai")]
    [EndpointSummary("AI Model Ayarlarını ve API Anahtarlarını Merkezi Veritabanında Sakla")]
    public async Task<IActionResult> SaveAiSettings([FromBody] SaveAiSettingsApiRequest request, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        if (string.IsNullOrWhiteSpace(request.SettingsJson))
        {
            return BadRequest(new { error = "Ayarlar boş olamaz." });
        }
        await _settingsRepo.SaveAiSettingsJsonAsync(resolvedShopId, request.SettingsJson, cancellationToken);
        return Ok(new { success = true, message = "AI ayarları ve API anahtarları veritabanına başarıyla kaydedildi!" });
    }

    [HttpGet("etsy/settings/credentials")]
    [EndpointSummary("Etsy App Geliştirici Anahtarlarını Getir (Maskeli)")]
    public async Task<IActionResult> GetEtsyAppCredentials([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var creds = await _settingsRepo.GetEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        if (creds is not null)
            return Ok(creds);

        var cfgKey = _config["Etsy:ApiKey"] ?? string.Empty;
        var cfgSecret = _config["Etsy:SharedSecret"] ?? string.Empty;
        var redirectUri = _config["Etsy:RedirectUri"] ?? "http://localhost:4200/settings/etsy-api";
        static string Mask(string s) => string.IsNullOrWhiteSpace(s) ? "" : (s.Length <= 8 ? "****" : $"{s[..4]}...{s[^4..]}");

        return Ok(new EtsyAppCredentialsRecord(
            resolvedShopId,
            Mask(cfgKey),
            Mask(cfgSecret),
            redirectUri,
            DateTimeOffset.UtcNow));
    }

    [HttpPost("etsy/settings/credentials")]
    [EndpointSummary("Etsy App Geliştirici Anahtarlarını Kaydet (Şifreli)")]
    public async Task<IActionResult> SaveEtsyAppCredentials([FromBody] SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default)
    {
        var saved = await _settingsRepo.SaveEtsyAppCredentialsAsync(request, cancellationToken);
        return Ok(saved);
    }

    [HttpGet("etsy/settings/telegram")]
    [EndpointSummary("Mağaza Telegram Bildirim Ayarlarını Getir (Maskeli)")]
    public async Task<IActionResult> GetTelegramSettings([FromQuery] string shopId = "53236321", CancellationToken ct = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var settings = await _settingsRepo.GetTelegramSettingsAsync(resolvedShopId, ct);
        return Ok(settings ?? new TelegramShopSettings(resolvedShopId, "", "", false, true, true, true, DateTimeOffset.UtcNow));
    }

    [HttpPost("etsy/settings/telegram")]
    [EndpointSummary("Mağaza Telegram Ayarlarını Kaydet (Şifreli)")]
    public async Task<IActionResult> SaveTelegramSettings([FromBody] SaveTelegramSettingsRequest request, CancellationToken ct = default)
    {
        var saved = await _settingsRepo.SaveTelegramSettingsAsync(request, ct);
        return Ok(saved);
    }

    [HttpGet("etsy/settings/carrier-sessions")]
    [EndpointSummary("Kargo Taşıyıcı Oturumlarını Getir (Maskeli)")]
    public async Task<IActionResult> GetCarrierSessions([FromQuery] string shopId = "53236321", CancellationToken ct = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var sessions = await _settingsRepo.GetCarrierSessionsAsync(resolvedShopId, ct);
        return Ok(sessions);
    }

    [HttpPost("etsy/settings/carrier-sessions")]
    [EndpointSummary("Kargo Taşıyıcı Oturumunu Kaydet (Şifreli)")]
    public async Task<IActionResult> SaveCarrierSession([FromBody] SaveCarrierSessionRequest request, CancellationToken ct = default)
    {
        var saved = await _settingsRepo.SaveCarrierSessionAsync(request, ct);
        return Ok(saved);
    }

    [HttpGet("etsy/shop/performance")]
    [EndpointSummary("Mağaza Performans Geçmişini Getir")]
    public async Task<IActionResult> GetShopPerformanceHistory([FromQuery] long shopId = 53236321, CancellationToken ct = default)
    {
        var history = await _perfRepo.GetByShopAsync(shopId, ct);
        return Ok(history);
    }
}
