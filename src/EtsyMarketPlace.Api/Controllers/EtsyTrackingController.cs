using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy/tracking")]
public class EtsyTrackingController : ControllerBase
{
    private readonly ITrackingRepository _repo;

    public EtsyTrackingController(ITrackingRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("")]
    [EndpointSummary("Takip Edilen Varlıkları Getir")]
    public async Task<IActionResult> GetTrackingItems(CancellationToken ct)
    {
        var items = await _repo.GetItemsAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id}/snapshots")]
    [EndpointSummary("Takip Öğesinin Geçmiş Anlık Görüntüleri")]
    public async Task<IActionResult> GetTrackingSnapshots(long id, CancellationToken ct)
    {
        var snapshots = await _repo.GetSnapshotsAsync(id, ct);
        return Ok(snapshots);
    }

    [HttpPost("capture")]
    [EndpointSummary("Yeni Takip Görüntüsü Kaydet")]
    public async Task<IActionResult> SaveTrackingCapture([FromBody] TrackingCapture capture, CancellationToken ct)
    {
        var item = await _repo.SaveCaptureAsync(capture, ct);
        return Ok(item);
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Takip Öğesini Sil")]
    public async Task<IActionResult> DeleteTrackingItem(long id, CancellationToken ct)
    {
        await _repo.DeleteItemAsync(id, ct);
        return Ok(new { success = true });
    }
}
