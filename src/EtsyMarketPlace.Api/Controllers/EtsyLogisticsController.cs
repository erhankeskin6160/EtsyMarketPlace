namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MediatR;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Application.Features.Logistics.Commands.ConnectCarrier;
using EtsyMarketPlace.Application.Features.Logistics.Commands.CreateCarrierShipment;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierQuotes;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierSessions;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetReadinessStates;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetSellerTaxonomyNodes;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetShippingProfiles;
using EtsyMarketPlace.Application.Features.Logistics.Queries.SearchHsCodes;
using EtsyMarketPlace.Application.Features.Logistics.Queries.SearchSellerTaxonomyNodes;

[ApiController]
[Route("api/etsy")]
public class EtsyLogisticsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    public EtsyLogisticsController(
        IConfiguration config,
        IMediator mediator)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("shipping-profiles")]
    [EndpointSummary("Mağazanın Etsy Kargo Profillerini Canlı Getir")]
    public async Task<IActionResult> GetEtsyShippingProfiles(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var request = new GetShippingProfilesRequest { ShopId = resolvedShopId };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("readiness-states")]
    [EndpointSummary("Mağazanın Etsy Hazırlık Durumlarını (readiness-state-definitions) Canlı Getir")]
    public async Task<IActionResult> GetEtsyReadinessStates(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var request = new GetReadinessStatesRequest { ShopId = resolvedShopId };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("taxonomy/nodes")]
    [EndpointSummary("Etsy Resmi Satıcı Kategori Ağacını (Seller Taxonomy) Getir")]
    public async Task<IActionResult> GetSellerTaxonomyNodes(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var request = new GetSellerTaxonomyNodesRequest { ShopId = resolvedShopId };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("taxonomy/search")]
    [EndpointSummary("Etsy Resmi Satıcı Kategori Ağacında Arama Yap")]
    public async Task<IActionResult> SearchSellerTaxonomyNodes(
        [FromQuery] string shopId = "53236321",
        [FromQuery] string q = "",
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var request = new SearchSellerTaxonomyNodesRequest
        {
            ShopId = resolvedShopId,
            Query = q,
            Limit = limit
        };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
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
        var request = new CreateCarrierShipmentRequest { Command = command };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("shipping/carrier-sessions")]
    [EndpointSummary("Kayıtlı Kargo Firması Oturumlarını ve Aktif Bearer Token Durumlarını Getir")]
    public async Task<IActionResult> GetCarrierSessions(CancellationToken cancellationToken = default)
    {
        var request = new GetCarrierSessionsRequest();
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
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

        var request = new ConnectCarrierRequest { Command = command };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("shipping/quotes")]
    [EndpointSummary("4 Taşıyıcıdan (Aras Global, ShipEntegra, Shiptomore, Navlungo) Canlı ve Tarife Kargo Tekliflerini Getir")]
    public async Task<IActionResult> GetCarrierQuotes(
        [FromBody] CarrierQuotesQuery query,
        CancellationToken cancellationToken = default)
    {
        var request = new GetCarrierQuotesRequest { Query = query ?? new CarrierQuotesQuery() };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("shipping/hs-codes")]
    [EndpointSummary("GTİP / HS Gümrük Tarife Kodlarını ve ABD HTS Kataloğunu Ara")]
    public async Task<IActionResult> SearchHsCodes(
        [FromQuery] string? query = null,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchHsCodesRequest { Query = query, Limit = limit };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
