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
    [EndpointSummary("Son 30 Günlük AI Token & Maliyet Özeti")]
    public async Task<IActionResult> GetAiUsageStats([FromQuery] string? provider, CancellationToken ct)
    {
        var stats = await _repo.GetSummaryStatsAsync(provider, DateTimeOffset.UtcNow.AddDays(-30), ct);
        return Ok(stats);
    }

    [HttpGet("history")]
    [EndpointSummary("AI Model Çağrı Geçmişi")]
    public async Task<IActionResult> GetAiUsageHistory([FromQuery] string? provider, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var history = await _repo.GetHistoryAsync(provider, null, Math.Clamp(limit <= 0 ? 100 : limit, 1, 300), ct);
        return Ok(history);
    }

    [HttpPost("")]
    [EndpointSummary("Yeni AI Çağrı Kaydı Ekle")]
    public async Task<IActionResult> RecordAiUsage([FromBody] AiUsageRecord record, CancellationToken ct)
    {
        var saved = await _repo.SaveUsageAsync(record, ct);
        return Ok(saved);
    }
}
