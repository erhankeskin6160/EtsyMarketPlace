namespace EtsyMarketPlace.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Settings;

[ApiController]
[Route("api")]
public class SettingsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    public SettingsController(
        IConfiguration config,
        IMediator mediator)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("settings/ai")]
    [EndpointSummary("Merkezi Veritabanından AI Model ve API Anahtarlarını Getir")]
    public async Task<IActionResult> GetAiSettings([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new GetAiSettingsRequest(resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("settings/ai")]
    [EndpointSummary("AI Model Ayarlarını ve API Anahtarlarını Merkezi Veritabanında Sakla")]
    public async Task<IActionResult> SaveAiSettings([FromBody] SaveAiSettingsApiRequest request, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        var response = await _mediator.Send(new SaveAiSettingsCommandRequest(resolvedShopId, request.SettingsJson), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("etsy/settings/credentials")]
    [EndpointSummary("Etsy App Geliştirici Anahtarlarını Getir (Maskeli)")]
    public async Task<IActionResult> GetEtsyAppCredentials([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var cfgKey = _config["Etsy:ApiKey"] ?? string.Empty;
        var cfgSecret = _config["Etsy:SharedSecret"] ?? string.Empty;
        var redirectUri = _config["Etsy:RedirectUri"] ?? "http://localhost:4200/settings/etsy-api";

        var response = await _mediator.Send(new GetEtsyCredentialsRequest(resolvedShopId, cfgKey, cfgSecret, redirectUri), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("etsy/settings/credentials")]
    [EndpointSummary("Etsy App Geliştirici Anahtarlarını Kaydet (Şifreli)")]
    public async Task<IActionResult> SaveEtsyAppCredentials([FromBody] SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new SaveEtsyCredentialsCommandRequest(request), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("etsy/settings/telegram")]
    [EndpointSummary("Mağaza Telegram Bildirim Ayarlarını Getir (Maskeli)")]
    public async Task<IActionResult> GetTelegramSettings([FromQuery] string shopId = "53236321", CancellationToken ct = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new GetTelegramSettingsRequest(resolvedShopId), ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("etsy/settings/telegram")]
    [EndpointSummary("Mağaza Telegram Ayarlarını Kaydet (Şifreli)")]
    public async Task<IActionResult> SaveTelegramSettings([FromBody] SaveTelegramSettingsRequest request, CancellationToken ct = default)
    {
        var response = await _mediator.Send(new SaveTelegramSettingsCommandRequest(request), ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("etsy/settings/carrier-sessions")]
    [EndpointSummary("Kargo Taşıyıcı Oturumlarını Getir (Maskeli)")]
    public async Task<IActionResult> GetCarrierSessions([FromQuery] string shopId = "53236321", CancellationToken ct = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new GetCarrierSessionsRequest(resolvedShopId), ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("etsy/settings/carrier-sessions")]
    [EndpointSummary("Kargo Taşıyıcı Oturumunu Kaydet (Şifreli)")]
    public async Task<IActionResult> SaveCarrierSession([FromBody] SaveCarrierSessionRequest request, CancellationToken ct = default)
    {
        var response = await _mediator.Send(new SaveCarrierSessionCommandRequest(request), ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("etsy/shop/performance")]
    [EndpointSummary("Mağaza Performans Geçmişini Getir")]
    public async Task<IActionResult> GetShopPerformanceHistory([FromQuery] long shopId = 53236321, CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetShopPerformanceHistoryRequest(shopId), ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
