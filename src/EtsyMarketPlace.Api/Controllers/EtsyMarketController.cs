namespace EtsyMarketPlace.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.MarketResearch;

[ApiController]
[Route("api/etsy/market")]
public class EtsyMarketController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMarketResearchService _marketResearchService;
    private readonly IMarketKeywordTrackingService _marketKeywordTrackingService;

    public EtsyMarketController(
        IConfiguration config,
        IMarketResearchService marketResearchService,
        IMarketKeywordTrackingService marketKeywordTrackingService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _marketResearchService = marketResearchService ?? throw new ArgumentNullException(nameof(marketResearchService));
        _marketKeywordTrackingService = marketKeywordTrackingService ?? throw new ArgumentNullException(nameof(marketKeywordTrackingService));
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

        var result = await _marketResearchService.SearchMarketAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("keywords/track")]
    [EndpointSummary("Kelimeyi takibe al ve pazar anlık görüntüsü (snapshot) kaydet")]
    public async Task<IActionResult> TrackKeyword([FromBody] TrackMarketKeywordRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Keyword))
        {
            return BadRequest(new { error = "Takip için keyword gerekli." });
        }

        var history = await _marketKeywordTrackingService.TrackKeywordAsync(request.Keyword, cancellationToken);
        return Ok(history);
    }

    [HttpGet("keywords")]
    [EndpointSummary("Takip edilen pazar kelimeleri ve snapshot geçmişleri")]
    public async Task<IActionResult> GetTrackedKeywords(CancellationToken cancellationToken)
    {
        var list = await _marketKeywordTrackingService.GetTrackedKeywordsAsync(cancellationToken);
        return Ok(list);
    }

    [HttpDelete("keywords")]
    [EndpointSummary("Takip edilen kelimeyi ve tüm snapshot geçmişini sil")]
    public async Task<IActionResult> DeleteTrackedKeyword([FromQuery] string keyword, CancellationToken cancellationToken)
    {
        var removed = await _marketKeywordTrackingService.DeleteKeywordAsync(keyword, cancellationToken);
        return Ok(new { success = removed });
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

        var report = await _marketResearchService.GenerateAiMarketReportAsync(command, cancellationToken);
        return Ok(report);
    }

    [HttpGet("competitor")]
    [EndpointSummary("Etsy v3 Rakip Mağaza Analizi (EverBee Satış/Ciro Tahmini, eRank LQS Denetimi ve AI Strateji)")]
    public async Task<IActionResult> AnalyzeCompetitorShop(
        [FromQuery] string shopNameOrUrl = "",
        [FromQuery] int limit = 50,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopNameOrUrl))
        {
            return BadRequest(new { error = "Rakip mağaza adı veya Etsy mağaza URL'si gereklidir." });
        }

        var resolvedShopId = ResolveShopId(shopId, _config);
        var result = await _marketResearchService.AnalyzeCompetitorShopAsync(
            shopNameOrUrl: shopNameOrUrl.Trim(),
            limit: limit,
            authShopId: resolvedShopId,
            cancellationToken: cancellationToken);
        return Ok(result);
    }

    [HttpGet("seasonal-trends")]
    [EndpointSummary("Yıllık E-Ticaret ve Etsy Dönemsel Trend / Q4 Takvim Radarı")]
    public IActionResult GetSeasonalTrends()
    {
        var trends = _marketResearchService.GetSeasonalTrends();
        return Ok(trends);
    }

    [HttpGet("listing/{listingId:long}/inventory")]
    [EndpointSummary("Etsy v3 İlan Envanter ve Varyasyon Bilgilerini Getirir")]
    public async Task<IActionResult> GetListingInventory(
        [FromRoute] long listingId,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        if (listingId <= 0) return BadRequest(new { error = "Geçersiz listingId" });
        var resolvedShopId = ResolveShopId(shopId, _config);
        var inventory = await _marketResearchService.GetListingInventoryAsync(listingId, resolvedShopId, cancellationToken);
        return Ok(inventory);
    }

    [HttpPost("listing/improve-lqs")]
    [EndpointSummary("Seçili AI Modeli veya SmartMarketEngine ile Listelemeyi 100/100 LQS Seviyesine Yükseltir")]
    public async Task<IActionResult> ImproveListingLqs(
        [FromBody] AiLqsImprovementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.Title))
        {
            return BadRequest(new { error = "LQS denetimi için ürün başlığı gereklidir." });
        }
        var result = await _marketResearchService.ImproveListingLqsWithAiAsync(command, cancellationToken);
        return Ok(result);
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
