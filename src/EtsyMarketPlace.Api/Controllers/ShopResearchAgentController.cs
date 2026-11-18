namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.Features.ShopResearchAgent;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

[ApiController]
[Authorize]
[Route("api/v{version:apiVersion}/etsy/research-agent")]
[Route("api/etsy/research-agent")]
public sealed class ShopResearchAgentController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;
    private readonly ShopAccessPolicy _shopAccessPolicy;

    public ShopResearchAgentController(
        IMediator mediator,
        IConfiguration config,
        ShopAccessPolicy shopAccessPolicy)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _shopAccessPolicy = shopAccessPolicy ?? throw new ArgumentNullException(nameof(shopAccessPolicy));
    }

    [HttpGet("dna")]
    [EndpointSummary("Kullanıcının mağazasına özel çıkarılan Mağaza DNA profilini getir (isteğe bağlı canlı yenileme ile)")]
    public async Task<IActionResult> GetShopDna(
        [FromQuery] string? shopId = null,
        [FromQuery] bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveIdentity(shopId, out var ownerUserId, out var resolvedShopId, out var errorResult))
            return errorResult!;

        var response = await _mediator.Send(new GetShopDnaQuery(ownerUserId, resolvedShopId, refresh), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("dna")]
    [EndpointSummary("Mağaza DNA kısıtlarını ve günlük/haftalık/aylık otomatik tarama takvimini güncelle")]
    public async Task<IActionResult> UpdateShopDna(
        [FromBody] UpdateShopDnaRequest request,
        [FromQuery] string? shopId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveIdentity(shopId, out var ownerUserId, out var resolvedShopId, out var errorResult))
            return errorResult!;

        var response = await _mediator.Send(new UpdateShopDnaCommand(ownerUserId, resolvedShopId, request), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("run")]
    [EndpointSummary("Sadece bu kullanıcının mağazası için kişiselleştirilmiş ürün araştırma ajanını çalıştır")]
    public async Task<IActionResult> RunResearch(
        [FromBody] RunShopResearchRequest? request,
        [FromQuery] string? shopId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveIdentity(shopId, out var ownerUserId, out var resolvedShopId, out var errorResult))
            return errorResult!;

        var enrichedRequest = EnrichAiCredentials(request);
        var response = await _mediator.Send(
            new RunShopResearchCommand(ownerUserId, resolvedShopId, ResearchTriggerType.Manual, enrichedRequest),
            cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("runs")]
    [EndpointSummary("Mağazaya ait geçmiş ürün araştırma oturumlarını listele")]
    public async Task<IActionResult> GetRuns(
        [FromQuery] string? shopId = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveIdentity(shopId, out var ownerUserId, out var resolvedShopId, out var errorResult))
            return errorResult!;

        var response = await _mediator.Send(new GetResearchRunsQuery(ownerUserId, resolvedShopId, limit), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("runs/{runId:guid}")]
    [EndpointSummary("Belirli bir araştırma oturumunu ve kanıtlı aday ürünlerini getir")]
    public async Task<IActionResult> GetRunById(
        Guid runId,
        [FromQuery] string? shopId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveIdentity(shopId, out var ownerUserId, out var resolvedShopId, out var errorResult))
            return errorResult!;

        var response = await _mediator.Send(new GetResearchRunByIdQuery(ownerUserId, resolvedShopId, runId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("candidates/{candidateId:guid}/decision")]
    [EndpointSummary("Aday ürün için Onayla / Reddet / Taslağa Al kararını mağaza hafızasına kaydet")]
    public async Task<IActionResult> RecordDecision(
        Guid candidateId,
        [FromBody] RecordCandidateDecisionRequest request,
        [FromQuery] string? shopId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveIdentity(shopId, out var ownerUserId, out var resolvedShopId, out var errorResult))
            return errorResult!;

        var response = await _mediator.Send(
            new RecordCandidateDecisionCommand(ownerUserId, resolvedShopId, candidateId, request),
            cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    private bool TryResolveIdentity(
        string? queryShopId,
        out string ownerUserId,
        out string resolvedShopId,
        out IActionResult? errorResult)
    {
        ownerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.Identity?.Name
            ?? string.Empty;

        resolvedShopId = ResolveShopId(queryShopId, _config);

        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            errorResult = Unauthorized(new { error = "Geçerli kullanıcı kimliği doğrulanamadı." });
            return false;
        }

        if (!_shopAccessPolicy.IsAllowed(resolvedShopId))
        {
            errorResult = StatusCode(403, new { error = "Bu mağaza için erişim yetkiniz bulunmuyor." });
            return false;
        }

        errorResult = null;
        return true;
    }

    private RunShopResearchRequest EnrichAiCredentials(RunShopResearchRequest? request)
    {
        var headerKey = Request.Headers.TryGetValue("X-Gemini-Api-Key", out var hKey) ? hKey.ToString().Trim() : null;
        var apiKey = !string.IsNullOrWhiteSpace(request?.ApiKey)
            ? request.ApiKey!.Trim()
            : !string.IsNullOrWhiteSpace(headerKey)
                ? headerKey
                : _config["Gemini:ApiKey"];

        return new RunShopResearchRequest(
            request?.FocusHint,
            request?.MaxCandidates ?? 10,
            request?.Model,
            apiKey);
    }
}
