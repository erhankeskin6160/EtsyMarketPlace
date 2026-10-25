namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MediatR;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.Features.Listings.Commands.AiOptimizeListing;
using EtsyMarketPlace.Application.Features.Listings.Commands.CreateListing;
using EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListing;
using EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListingState;
using EtsyMarketPlace.Application.Features.Listings.Queries.GetListingAudits;
using EtsyMarketPlace.Application.Features.Listings.Queries.GetShopListings;

[ApiController]
[Route("api/etsy")]
public class EtsyListingsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    public EtsyListingsController(
        IConfiguration config,
        IMediator mediator)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("shop/listings")]
    [EndpointSummary("Mağazanın Aktif Listinglerini ve Yapısal SEO Analizini Getir")]
    public async Task<IActionResult> GetShopListings([FromQuery] string shopId = "53236321", [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var request = new GetShopListingsRequest { ShopId = resolvedShopId, Limit = limit };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("listings/{listingId}/ai-optimize")]
    [EndpointSummary("Listing İçin Yapay Zeka SEO ve Başlık/Tag Optimizasyonu")]
    public async Task<IActionResult> AiOptimizeListing(
        string listingId,
        [FromQuery] string? shopId,
        [FromBody] OptimizeListingApiRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedShopId = !string.IsNullOrWhiteSpace(request?.ShopId)
            ? request.ShopId.Trim()
            : ResolveShopId(shopId, _config);

        var apiKey = !string.IsNullOrWhiteSpace(request?.ApiKey)
            ? request.ApiKey.Trim()
            : Request.Headers.TryGetValue("X-Gemini-Api-Key", out var hKey) && !string.IsNullOrWhiteSpace(hKey)
                ? hKey.ToString().Trim()
                : _config["Gemini:ApiKey"] ?? string.Empty;

        var command = new OptimizeListingCommandDto(
            resolvedShopId,
            request?.Title,
            request?.Description,
            request?.Tags,
            request?.TargetKeyword,
            request?.DescriptionStyle,
            request?.FocusKeywords,
            request?.TargetBuyerPersona,
            request?.Tone,
            request?.Model,
            apiKey,
            request?.Provider);

        var req = new AiOptimizeListingRequest
        {
            ListingId = listingId,
            ShopId = resolvedShopId,
            Command = command
        };

        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("listings/{listingId}")]
    [EndpointSummary("İlanı Başlık, Etiket ve Açıklama ile Doğrudan Etsy'de Güncelle")]
    public async Task<IActionResult> UpdateListing(
        string listingId,
        [FromBody] UpdateListingApiRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        var reqDto = new UpdateListingRequestDto(
            resolvedShopId,
            request.Title,
            request.Description,
            request.Tags,
            request.Materials);

        var req = new UpdateListingCommandRequest
        {
            ListingId = listingId,
            ShopId = resolvedShopId,
            Dto = reqDto
        };

        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("listings")]
    [EndpointSummary("Hızlı Ürün Ekleme (Fast Creator) İle Doğrudan Etsy'de Taslak/Canlı İlan Oluştur")]
    public async Task<IActionResult> CreateListing(
        [FromBody] CreateListingApiRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        var command = new FastListingPublishCommand(
            ShopId: resolvedShopId,
            Title: request.Title,
            Description: request.Description,
            Price: request.Price,
            Quantity: request.Quantity,
            TaxonomyId: request.TaxonomyId,
            ShippingProfileId: request.ShippingProfileId,
            ReadinessStateId: request.ReadinessStateId,
            IsDigital: request.IsDigital,
            Tags: request.Tags,
            Materials: request.Materials,
            State: request.State,
            WhoMade: request.WhoMade,
            WhenMade: request.WhenMade,
            Images: request.Images?.Select(i => new ListingImageInput(i.DataUrl, i.Url, i.Rank)).ToList(),
            Variations: request.Variations?.Select(v => new ListingVariationInput(v.Key, v.Price, v.Quantity, v.Active, v.ImageRank)).ToList(),
            VariationGroups: request.VariationGroups?.Select(g => new ListingVariationGroupInput(g.Name, g.Values)).ToList()
        );

        var req = new CreateListingCommandRequest { Command = command };
        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPatch("listings/{listingId:long}/state")]
    [EndpointSummary("Etsy İlan Durumunu Güncelle (draft / active / inactive)")]
    public async Task<IActionResult> UpdateListingState(
        long listingId,
        [FromBody] UpdateListingStateApiRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var req = new UpdateListingStateCommandRequest
        {
            ListingId = listingId,
            ShopId = resolvedShopId,
            State = request.State
        };

        var response = await _mediator.Send(req, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("listings/audits")]
    [EndpointSummary("Daha Önce Yapılan Listing AI Denetim Geçmişini Getir")]
    public async Task<IActionResult> GetListingAudits([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var request = new GetListingAuditsRequest { ShopId = resolvedShopId };
        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}
