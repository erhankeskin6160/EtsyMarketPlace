namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.Features.AiUsage.Commands.RecordAiUsage;
using EtsyMarketPlace.Application.Features.AiUsage.Queries.GetAiUsageHistory;
using EtsyMarketPlace.Application.Features.AiUsage.Queries.GetAiUsageStats;

[ApiController]
[Route("api/etsy/ai-usage")]
public class EtsyAiUsageController : ControllerBase
{
    private readonly IMediator _mediator;

    public EtsyAiUsageController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("stats")]
    [EndpointSummary("AI Token & Maliyet Özeti")]
    public async Task<IActionResult> GetAiUsageStats(
        [FromQuery] string? provider,
        [FromQuery] string? period = "30d",
        CancellationToken ct = default)
    {
        var request = new GetAiUsageStatsRequest { Provider = provider, Period = period };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("history")]
    [EndpointSummary("AI Model Çağrı Geçmişi")]
    public async Task<IActionResult> GetAiUsageHistory(
        [FromQuery] string? provider,
        [FromQuery] string? period = "30d",
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var request = new GetAiUsageHistoryRequest { Provider = provider, Period = period, Limit = limit };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("balances")]
    [EndpointSummary("Yapay Zeka Markalarının Canlı Bakiye, Kalan TL/USD ve Token Kullanım Özeti")]
    public async Task<IActionResult> GetAiBalances(
        [FromServices] IAiBalanceService balanceService,
        [FromQuery] string shopId = "53236321",
        [FromQuery] decimal exchangeRate = 40.0m,
        [FromQuery] string? period = "30d",
        CancellationToken ct = default)
    {
        var balances = await balanceService.GetProviderBalancesAsync(shopId, exchangeRate, period, ct);
        return Ok(balances);
    }

    [HttpPost("")]
    [EndpointSummary("Yeni AI Çağrı Kaydı Ekle")]
    public async Task<IActionResult> RecordAiUsage([FromBody] AiUsageRecord record, CancellationToken ct)
    {
        var request = new RecordAiUsageRequest { Record = record };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
