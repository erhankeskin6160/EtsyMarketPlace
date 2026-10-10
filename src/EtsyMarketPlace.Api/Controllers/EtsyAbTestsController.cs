using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Api.Models;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy/ab-tests")]
public class EtsyAbTestsController : ControllerBase
{
    private readonly IAbTestRepository _repo;

    public EtsyAbTestsController(IAbTestRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("")]
    [EndpointSummary("A/B Test Deneylerini Listele")]
    public async Task<IActionResult> GetAbTests([FromQuery] string? listingId, CancellationToken ct)
    {
        var tests = string.IsNullOrWhiteSpace(listingId)
            ? await _repo.GetRecentAsync(50, ct)
            : await _repo.GetByListingIdAsync(listingId, ct);
        return Ok(tests);
    }

    [HttpPost("")]
    [EndpointSummary("Yeni A/B Test Başlat")]
    public async Task<IActionResult> CreateAbTest([FromBody] SaveAbTestExperiment experiment, CancellationToken ct)
    {
        var saved = await _repo.SaveAsync(experiment, ct);
        return Ok(saved);
    }

    [HttpPut("{id}/status")]
    [EndpointSummary("A/B Test Durumunu Güncelle")]
    public async Task<IActionResult> UpdateAbTestStatus(long id, [FromBody] UpdateAbTestStatusRequest request, CancellationToken ct)
    {
        var updated = await _repo.UpdateStatusAsync(id, request.Status, ct);
        return updated != null ? Ok(updated) : NotFound();
    }

    [HttpDelete("{id}")]
    [EndpointSummary("A/B Test Deneyini Sil")]
    public async Task<IActionResult> DeleteAbTest(long id, CancellationToken ct)
    {
        var deleted = await _repo.DeleteAsync(id, ct);
        return deleted ? Ok(new { success = true }) : NotFound();
    }
}
