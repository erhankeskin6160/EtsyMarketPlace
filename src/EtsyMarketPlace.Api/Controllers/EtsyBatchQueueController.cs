namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.ClearCompletedBatch;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.EnqueueBatchItems;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.ProcessBatchItem;
using EtsyMarketPlace.Application.Features.BatchQueue.Queries.GetBatchQueue;

[ApiController]
[Route("api/etsy/batch-queue")]
public class EtsyBatchQueueController : ControllerBase
{
    private readonly IMediator _mediator;

    public EtsyBatchQueueController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("")]
    [EndpointSummary("Batch Kuyruğundaki İlanları Getir")]
    public async Task<IActionResult> GetBatchQueue([FromQuery] string? status, CancellationToken ct)
    {
        var request = new GetBatchQueueRequest { Status = status };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("enqueue")]
    [EndpointSummary("Toplu Optimizasyon Kuyruğuna İlan Ekle")]
    public async Task<IActionResult> EnqueueBatchItems([FromBody] BatchEnqueueRequest request, CancellationToken ct)
    {
        var cmd = new EnqueueBatchItemsRequest { Items = request?.Items ?? [] };
        var response = await _mediator.Send(cmd, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("{id}/process")]
    [EndpointSummary("Batch Kuyruk Öğesini İşlenmiş Olarak Güncelle")]
    public async Task<IActionResult> ProcessBatchItem(long id, [FromBody] BatchProcessRequest req, CancellationToken ct)
    {
        var cmd = new ProcessBatchItemRequest
        {
            Id = id,
            OptimizedTitle = req.OptimizedTitle,
            OptimizedDescription = req.OptimizedDescription,
            OptimizedTags = req.OptimizedTags,
            OverallScore = req.OverallScore,
            Status = req.Status
        };
        var response = await _mediator.Send(cmd, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpDelete("completed")]
    [EndpointSummary("Tamamlanan Kuyruk Öğelerini Temizle")]
    public async Task<IActionResult> ClearCompletedBatch(CancellationToken ct)
    {
        var request = new ClearCompletedBatchRequest();
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
