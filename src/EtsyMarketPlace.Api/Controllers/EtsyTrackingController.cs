namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using EtsyMarketPlace.Application.Features.Tracking.Commands.DeleteTrackingItem;
using EtsyMarketPlace.Application.Features.Tracking.Commands.SaveTrackingCapture;
using EtsyMarketPlace.Application.Features.Tracking.Queries.GetTrackingItems;
using EtsyMarketPlace.Application.Features.Tracking.Queries.GetTrackingSnapshots;

[ApiController]
[Route("api/etsy/tracking")]
public class EtsyTrackingController : ControllerBase
{
    private readonly IMediator _mediator;

    public EtsyTrackingController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("")]
    [EndpointSummary("Takip Edilen Varlıkları Getir")]
    public async Task<IActionResult> GetTrackingItems(CancellationToken ct)
    {
        var request = new GetTrackingItemsRequest();
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("{id}/snapshots")]
    [EndpointSummary("Takip Öğesinin Geçmiş Anlık Görüntüleri")]
    public async Task<IActionResult> GetTrackingSnapshots(long id, CancellationToken ct)
    {
        var request = new GetTrackingSnapshotsRequest { TrackingItemId = id };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("capture")]
    [EndpointSummary("Yeni Takip Görüntüsü Kaydet")]
    public async Task<IActionResult> SaveTrackingCapture([FromBody] TrackingCapture capture, CancellationToken ct)
    {
        var request = new SaveTrackingCaptureRequest { Capture = capture };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Takip Öğesini Sil")]
    public async Task<IActionResult> DeleteTrackingItem(long id, CancellationToken ct)
    {
        var request = new DeleteTrackingItemRequest { Id = id };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
