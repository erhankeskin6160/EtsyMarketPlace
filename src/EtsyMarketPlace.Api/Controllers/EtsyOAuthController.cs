namespace EtsyMarketPlace.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.Features.OAuth;

[ApiController]
[Route("api/etsy")]
public class EtsyOAuthController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    public EtsyOAuthController(
        IConfiguration config,
        IMediator mediator)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost("token")]
    [EndpointSummary("Etsy OAuth Token Aktarımı")]
    public async Task<IActionResult> ImportEtsyToken([FromBody] EtsyTokenImportRequest request, CancellationToken cancellationToken)
    {
        var cmd = new ImportEtsyTokenCommand(
            request.ShopId,
            request.AccessToken,
            request.RefreshToken,
            request.AccessTokenExpiresAt,
            request.TokenType ?? "Bearer");

        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("token/status")]
    [EndpointSummary("Etsy Token Durum ve Geçerlilik Kontrolü")]
    public async Task<IActionResult> GetEtsyTokenStatus([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new GetEtsyTokenStatusQuery(resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("token/refresh")]
    [EndpointSummary("Etsy Token Yenileme (Refresh Token)")]
    public async Task<IActionResult> RefreshEtsyToken([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new RefreshEtsyTokenCommand(resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("oauth/connect-url")]
    [EndpointSummary("Etsy OAuth v3 PKCE Yetkilendirme Bağlantısı Üret")]
    public async Task<IActionResult> GetEtsyOAuthConnectUrl([FromQuery] string shopId = "53236321", [FromQuery] string? redirectUri = null, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new GetEtsyOAuthConnectUrlQuery(resolvedShopId, redirectUri), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("oauth/exchange-code")]
    [EndpointSummary("Etsy OAuth Yetki Kodunu Token'a Dönüştür (Code Exchange)")]
    public async Task<IActionResult> ExchangeEtsyOAuthCode([FromBody] ExchangeCodeApiRequest request, CancellationToken cancellationToken)
    {
        var cmd = new ExchangeEtsyOAuthCodeCommand(
            request.ShopId,
            request.Code,
            request.State,
            request.CodeVerifier,
            request.RedirectUri);

        var response = await _mediator.Send(cmd, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
