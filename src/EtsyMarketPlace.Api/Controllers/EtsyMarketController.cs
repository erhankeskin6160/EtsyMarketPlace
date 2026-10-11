namespace EtsyMarketPlace.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Application.MarketResearch;

[ApiController]
[Route("api/etsy/market")]
public class EtsyMarketController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMarketResearchService _marketResearchService;

    public EtsyMarketController(
        IConfiguration config,
        IMarketResearchService marketResearchService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _marketResearchService = marketResearchService ?? throw new ArgumentNullException(nameof(marketResearchService));
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
}
