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

        try
        {
            command.ShopId = ResolveShopId(command.ShopId, _config);
            var result = await _orchestratorService.CreateShipmentAsync(command, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new CreateCarrierShipmentResult
            {
                Success = false,
                ReceiptId = command.ReceiptId,
                CarrierKey = command.CarrierKey,
                ErrorMessage = $"Gönderi oluşturulurken sunucu hatası oluştu: {ex.Message}"
            });
        }
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

    [HttpPost("shipping/quotes")]
    [EndpointSummary("4 Taşıyıcıdan (Aras Global, ShipEntegra, Shiptomore, Navlungo) Canlı ve Tarife Kargo Tekliflerini Getir")]
    public async Task<IActionResult> GetCarrierQuotes(
        [FromBody] CarrierQuotesQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _orchestratorService.GetCarrierQuotesAsync(query ?? new CarrierQuotesQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipping/hs-codes")]
    [EndpointSummary("GTİP / HS Gümrük Tarife Kodlarını ve ABD HTS Kataloğunu Ara")]
    public IActionResult SearchHsCodes(
        [FromQuery] string? query = null,
        [FromQuery] int limit = 25)
    {
        var items = _orchestratorService.SearchHsCodes(query, limit);
        return Ok(new
        {
            count = items.Count,
            results = items
        });
    }
}
