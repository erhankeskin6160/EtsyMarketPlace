using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Api.Models;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy/batch-queue")]
public class EtsyBatchQueueController : ControllerBase
{
    private readonly IBatchQueueRepository _repo;

    public EtsyBatchQueueController(IBatchQueueRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("")]
    [EndpointSummary("Batch Kuyruğundaki İlanları Getir")]
    public async Task<IActionResult> GetBatchQueue([FromQuery] string? status, CancellationToken ct)
    {
        var items = string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
            ? await _repo.GetPendingAsync(100, ct)
            : await _repo.GetAllAsync(200, ct);
        return Ok(items);
    }

    [HttpPost("enqueue")]
    [EndpointSummary("Toplu Optimizasyon Kuyruğuna İlan Ekle")]
    public async Task<IActionResult> EnqueueBatchItems([FromBody] BatchEnqueueRequest request, CancellationToken ct)
    {
        var enqueued = await _repo.EnqueueBatchAsync(request.Items, ct);
        return Ok(enqueued);
    }

    [HttpPost("{id}/process")]
    [EndpointSummary("Batch Kuyruk Öğesini İşlenmiş Olarak Güncelle")]
    public async Task<IActionResult> ProcessBatchItem(long id, [FromBody] BatchProcessRequest req, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing == null) return NotFound();

        var updated = existing with
        {
            OptimizedTitle = req.OptimizedTitle ?? existing.OptimizedTitle,
            OptimizedDescription = req.OptimizedDescription ?? existing.OptimizedDescription,
            OptimizedTags = req.OptimizedTags ?? existing.OptimizedTags,
            OverallScore = req.OverallScore,
            Status = req.Status,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        var result = await _repo.UpdateItemAsync(updated, ct);
        return Ok(result);
    }

    [HttpDelete("completed")]
    [EndpointSummary("Tamamlanan Kuyruk Öğelerini Temizle")]
    public async Task<IActionResult> ClearCompletedBatch(CancellationToken ct)
    {
        var count = await _repo.ClearCompletedAsync(ct);
        return Ok(new { success = true, cleared = count });
    }
}
