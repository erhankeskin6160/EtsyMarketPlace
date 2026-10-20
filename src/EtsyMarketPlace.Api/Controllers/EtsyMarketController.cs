namespace EtsyMarketPlace.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using MediatR;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.Features.MarketResearch;

[ApiController]
[Route("api/etsy/market")]
public class EtsyMarketController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    public EtsyMarketController(
        IConfiguration config,
        IMediator mediator)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpGet("search")]
    [EndpointSummary("Etsy v3 Canlı Pazar Araması, Rakip Listelemeleri, SEO & Fırsat Puanları")]
    public async Task<IActionResult> SearchMarket(
        [FromQuery] string keyword = "",
        [FromQuery] int limit = 50,
        [FromQuery] string sortBy = "market_score",
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var query = new MarketSearchQuery(
            Keyword: keyword,
            Limit: limit,
            SortBy: sortBy,
            ShopId: resolvedShopId
        );

        var response = await _mediator.Send(new SearchMarketQuery(query), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("keywords/track")]
    [EndpointSummary("Kelimeyi takibe al ve pazar anlık görüntüsü (snapshot) kaydet")]
    public async Task<IActionResult> TrackKeyword([FromBody] TrackMarketKeywordRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new TrackMarketKeywordCommand(request?.Keyword ?? string.Empty), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("keywords")]
    [EndpointSummary("Takip edilen pazar kelimeleri ve snapshot geçmişleri")]
    public async Task<IActionResult> GetTrackedKeywords(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetTrackedKeywordsQuery(), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpDelete("keywords")]
    [EndpointSummary("Takip edilen kelimeyi ve tüm snapshot geçmişini sil")]
    public async Task<IActionResult> DeleteTrackedKeyword([FromQuery] string keyword, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteTrackedKeywordCommand(keyword), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("ai-report")]
    [EndpointSummary("Aranan niş ve canlı rakip verileri üzerinden AI / Akıllı Pazar Strateji Raporu üretir")]
    public async Task<IActionResult> GenerateAiMarketReport(
        [FromBody] MarketAiReportApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Keyword))
        {
            return BadRequest(new { error = "Pazar strateji raporu için keyword gereklidir." });
        }

        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        var apiKey = !string.IsNullOrWhiteSpace(request.ApiKey)
            ? request.ApiKey.Trim()
            : Request.Headers.TryGetValue("X-Gemini-Api-Key", out var hKey) && !string.IsNullOrWhiteSpace(hKey)
                ? hKey.ToString().Trim()
                : _config["Gemini:ApiKey"] ?? string.Empty;

        var command = new MarketAiReportCommand(
            Keyword: request.Keyword,
            ShopId: resolvedShopId,
            AveragePrice: request.AveragePrice,
            MinPrice: request.MinPrice,
            MaxPrice: request.MaxPrice,
            MedianPrice: request.MedianPrice,
            AverageFavorites: request.AverageFavorites,
            AverageViews: request.AverageViews,
            OpportunityScore: request.OpportunityScore,
            TotalEstimatedMarketRevenue: request.TotalEstimatedMarketRevenue,
            UniqueShopCount: request.UniqueShopCount,
            TotalListings: request.TotalListings,
            TopShopName: request.TopShopName ?? "-",
            TopShopSales: request.TopShopSales,
            TopCompetitorTitles: request.TopCompetitorTitles,
            TopTags: request.TopTags,
            ApiKey: apiKey,
            Model: request.Model,
            Provider: request.Provider
        );

        var response = await _mediator.Send(new GenerateAiMarketReportCommand(command), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("competitor")]
    [EndpointSummary("Etsy v3 Rakip Mağaza Analizi (EverBee Satış/Ciro Tahmini, eRank LQS Denetimi ve AI Strateji)")]
    public async Task<IActionResult> AnalyzeCompetitorShop(
        [FromQuery] string shopNameOrUrl = "",
        [FromQuery] int limit = 50,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new AnalyzeCompetitorShopQuery(shopNameOrUrl, limit, resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("seasonal-trends")]
    [EndpointSummary("Yıllık E-Ticaret ve Etsy Dönemsel Trend / Q4 Takvim Radarı")]
    public async Task<IActionResult> GetSeasonalTrends(CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new GetSeasonalTrendsQuery(), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("listing/{listingId:long}/inventory")]
    [EndpointSummary("Etsy v3 İlan Envanter ve Varyasyon Bilgilerini Getirir")]
    public async Task<IActionResult> GetListingInventory(
        [FromRoute] long listingId,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var response = await _mediator.Send(new GetListingInventoryQuery(listingId, resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("listing/improve-lqs")]
    [EndpointSummary("Seçili AI Modeli veya SmartMarketEngine ile Listelemeyi 100/100 LQS Seviyesine Yükseltir")]
    public async Task<IActionResult> ImproveListingLqs(
        [FromBody] AiLqsImprovementCommand command,
        CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new ImproveListingLqsCommand(command), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}

public sealed record TrackMarketKeywordRequest(string Keyword);

public sealed class MarketAiReportApiRequest
{
    public string Keyword { get; set; } = string.Empty;
    public string? ShopId { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public decimal MedianPrice { get; set; }
    public double AverageFavorites { get; set; }
    public double AverageViews { get; set; }
    public int OpportunityScore { get; set; }
    public decimal TotalEstimatedMarketRevenue { get; set; }
    public int UniqueShopCount { get; set; }
    public int TotalListings { get; set; }
    public string? TopShopName { get; set; }
    public int TopShopSales { get; set; }
    public List<string>? TopCompetitorTitles { get; set; }
    public List<string>? TopTags { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
}
