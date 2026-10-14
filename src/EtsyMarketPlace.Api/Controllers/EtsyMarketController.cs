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
}

public sealed record TrackMarketKeywordRequest(string Keyword);
