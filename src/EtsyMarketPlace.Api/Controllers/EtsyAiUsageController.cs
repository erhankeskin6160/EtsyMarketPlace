using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.AiUsage;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy/ai-usage")]
public class EtsyAiUsageController : ControllerBase
{
    private readonly IAiUsageRepository _repo;

    public EtsyAiUsageController(IAiUsageRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("stats")]
    [EndpointSummary("AI Token & Maliyet Özeti")]
    public async Task<IActionResult> GetAiUsageStats(
        [FromQuery] string? provider,
        [FromQuery] string? period = "30d",
        CancellationToken ct = default)
    {
        var (since, until) = AiPeriodHelper.ResolveDateRange(period);
        var stats = await _repo.GetSummaryStatsAsync(provider, since, until, cancellationToken: ct);
        return Ok(stats);
    }

    [HttpGet("history")]
    [EndpointSummary("AI Model Çağrı Geçmişi")]
    public async Task<IActionResult> GetAiUsageHistory(
        [FromQuery] string? provider,
        [FromQuery] string? period = "30d",
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var (since, until) = AiPeriodHelper.ResolveDateRange(period);
        var history = await _repo.GetHistoryAsync(provider, since, Math.Clamp(limit <= 0 ? 100 : limit, 1, 1000), until, ct);
        return Ok(history);
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
        var saved = await _repo.SaveUsageAsync(record, ct);
        return Ok(saved);
    }
}
