using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.ListingOptimization;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy")]
public class EtsyListingsController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyListingService _listingService;
    private readonly IAiListingOptimizerUseCase _aiOptimizer;
    private readonly IFastListingPublishService _publishService;

    public EtsyListingsController(
        IConfiguration config,
        IEtsyListingService listingService,
        IAiListingOptimizerUseCase aiOptimizer,
        IFastListingPublishService publishService)
    {
        _config = config;
        _listingService = listingService;
        _aiOptimizer = aiOptimizer;
        _publishService = publishService;
    }

    [HttpGet("shop/listings")]
    [EndpointSummary("Mağazanın Aktif Listinglerini ve Yapısal SEO Analizini Getir")]
    public async Task<IActionResult> GetShopListings([FromQuery] string shopId = "53236321", [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedShopId = ResolveShopId(shopId, _config);
            var items = await _listingService.GetShopListingsAsync(resolvedShopId, limit, cancellationToken);
            return Ok(items);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("listings/{listingId}/ai-optimize")]
    [EndpointSummary("Listing İçin Yapay Zeka SEO ve Başlık/Tag Optimizasyonu")]
    public async Task<IActionResult> AiOptimizeListing(
        string listingId,
        [FromQuery] string? shopId,
        [FromBody] OptimizeListingApiRequest request,
        CancellationToken cancellationToken)
    {
        try
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

            var result = await _aiOptimizer.OptimizeListingAsync(listingId, resolvedShopId, command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"AI optimizasyon hatası: {ex.Message}" });
        }
    }

    [HttpPut("listings/{listingId}")]
    [EndpointSummary("İlanı Başlık, Etiket ve Açıklama ile Doğrudan Etsy'de Güncelle")]
    public async Task<IActionResult> UpdateListing(
        string listingId,
        [FromBody] UpdateListingApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolvedShopId = ResolveShopId(request.ShopId, _config);
            var reqDto = new UpdateListingRequestDto(
                resolvedShopId,
                request.Title,
                request.Description,
                request.Tags,
                request.Materials);

            var result = await _listingService.UpdateListingAsync(resolvedShopId, listingId, reqDto, cancellationToken);
            return Ok(new
            {
                success = true,
                listingId = result.ListingId,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
            Variations: request.Variations?.Select(v => new ListingVariationInput(v.Key, v.Price, v.Quantity, v.Active)).ToList(),
            VariationGroups: request.VariationGroups?.Select(g => new ListingVariationGroupInput(g.Name, g.Values)).ToList()
        );

        var result = await _publishService.PublishListingAsync(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(result);
    }

    [HttpPatch("listings/{listingId:long}/state")]
    [EndpointSummary("Etsy İlan Durumunu Güncelle (draft / active / inactive)")]
    public async Task<IActionResult> UpdateListingState(
        long listingId,
        [FromBody] UpdateListingStateApiRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedShopId = ResolveShopId(shopId, _config);
            var result = await _listingService.UpdateListingStateAsync(resolvedShopId, listingId, request.State, cancellationToken);
            return Ok(new
            {
                success = true,
                listingId,
                state = result.State,
                message = result.Message
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("listings/audits")]
    [EndpointSummary("Daha Önce Yapılan Listing AI Denetim Geçmişini Getir")]
    public async Task<IActionResult> GetListingAudits([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var audits = await _listingService.GetListingAuditsAsync(resolvedShopId, cancellationToken);
        return Ok(audits);
    }
}
