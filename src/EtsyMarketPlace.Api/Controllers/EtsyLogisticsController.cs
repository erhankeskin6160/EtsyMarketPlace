namespace EtsyMarketPlace.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.Shipping;

[ApiController]
[Route("api/etsy")]
public class EtsyLogisticsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyLogisticsService _logisticsService;
    private readonly IShipmentOrchestratorService _orchestratorService;

    public EtsyLogisticsController(
        IConfiguration config,
        IEtsyLogisticsService logisticsService,
        IShipmentOrchestratorService orchestratorService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logisticsService = logisticsService ?? throw new ArgumentNullException(nameof(logisticsService));
        _orchestratorService = orchestratorService ?? throw new ArgumentNullException(nameof(orchestratorService));
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

    [HttpPost("shipping/create-shipment")]
    [EndpointSummary("Seçilen Kargo Firmasında Gönderi ve Takip Kodu Oluştur")]
    public async Task<IActionResult> CreateShipment(
        [FromBody] CreateCarrierShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null || command.ReceiptId <= 0)
        {
            return BadRequest(new { success = false, message = "Geçersiz gönderi oluşturma isteği." });
        }

        command.ShopId = ResolveShopId(command.ShopId, _config);
        var result = await _orchestratorService.CreateShipmentAsync(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("shipping/connect-carrier")]
    [EndpointSummary("Kargo Firması Giriş ve API Kimlik Bilgilerini Doğrula ve Kaydet")]
    public async Task<IActionResult> ConnectCarrier(
        [FromBody] ConnectCarrierCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.CarrierKey))
        {
            return BadRequest(new { success = false, message = "Kargo firması belirtilmelidir." });
        }

        var result = await _orchestratorService.ConnectCarrierAsync(command, cancellationToken);
        return Ok(result);
    }
}
