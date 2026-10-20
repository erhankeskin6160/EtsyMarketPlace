namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.Features.AbTests.Commands.CreateAbTest;
using EtsyMarketPlace.Application.Features.AbTests.Commands.DeleteAbTest;
using EtsyMarketPlace.Application.Features.AbTests.Commands.UpdateAbTestStatus;
using EtsyMarketPlace.Application.Features.AbTests.Queries.GetAbTests;

[ApiController]
[Route("api/etsy/ab-tests")]
public class EtsyAbTestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EtsyAbTestsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("")]
    [EndpointSummary("A/B Test Deneylerini Listele")]
    public async Task<IActionResult> GetAbTests([FromQuery] string? listingId, CancellationToken ct)
    {
        var request = new GetAbTestsRequest { ListingId = listingId, Limit = 50 };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("")]
    [EndpointSummary("Yeni A/B Test Başlat")]
    public async Task<IActionResult> CreateAbTest([FromBody] SaveAbTestExperiment experiment, CancellationToken ct)
    {
        var request = new CreateAbTestRequest { Experiment = experiment };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("{id}/status")]
    [EndpointSummary("A/B Test Durumunu Güncelle")]
    public async Task<IActionResult> UpdateAbTestStatus(long id, [FromBody] EtsyMarketPlace.Api.Models.UpdateAbTestStatusRequest request, CancellationToken ct)
    {
        var cmd = new EtsyMarketPlace.Application.Features.AbTests.Commands.UpdateAbTestStatus.UpdateAbTestStatusRequest
        {
            Id = id,
            Status = request.Status
        };
        var response = await _mediator.Send(cmd, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{id}")]
    [EndpointSummary("A/B Test Deneyini Sil")]
    public async Task<IActionResult> DeleteAbTest(long id, CancellationToken ct)
    {
        var request = new DeleteAbTestRequest { Id = id };
        var response = await _mediator.Send(request, ct);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
