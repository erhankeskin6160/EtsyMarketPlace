namespace EtsyMarketPlace.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.Shipping;

[ApiController]
[Route("api/etsy")]
public class EtsyLogisticsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyLogisticsService _logisticsService;

    public EtsyLogisticsController(
        IConfiguration config,
        IEtsyLogisticsService logisticsService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logisticsService = logisticsService ?? throw new ArgumentNullException(nameof(logisticsService));
    }

    [HttpGet("shipping-profiles")]
    [EndpointSummary("Mağazanın Etsy Kargo Profillerini Canlı Getir")]
    public async Task<IActionResult> GetEtsyShippingProfiles(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var profiles = await _logisticsService.GetShippingProfilesAsync(resolvedShopId, cancellationToken);
        return Ok(new
        {
            count = profiles.Count,
            results = profiles
        });
    }

    [HttpGet("readiness-states")]
    [EndpointSummary("Mağazanın Etsy Hazırlık Durumlarını (readiness-state-definitions) Canlı Getir")]
    public async Task<IActionResult> GetEtsyReadinessStates(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var states = await _logisticsService.GetReadinessStatesAsync(resolvedShopId, cancellationToken);
        return Ok(new
        {
            count = states.Count,
            results = states
        });
    }

    [HttpGet("taxonomy/nodes")]
    [EndpointSummary("Etsy Resmi Satıcı Kategori Ağacını (Seller Taxonomy) Getir")]
    public async Task<IActionResult> GetSellerTaxonomyNodes(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var nodes = await _logisticsService.GetSellerTaxonomyNodesAsync(resolvedShopId, cancellationToken);
        return Ok(new
        {
            count = nodes.Count,
            results = nodes
        });
    }
}
