namespace EtsyMarketPlace.Application.Tests;

using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.MarketResearch;
using Xunit;

public sealed class MarketResearchServiceTests
{
    private sealed class FakeEtsyMarketApiClient : IEtsyMarketApiClient
    {
        public string RawJson { get; set; } = """
        {
            "count": 2,
            "results": [
                {
                    "listing_id": 111,
                    "shop_id": 10,
                    "title": "Dragon Lamp 3D Printed Night Light Artisan Decor Fantasy",
                    "description": "Very long description that describes this amazing dragon lamp with lots of details about how it was made and materials.",
                    "views": 500,
                    "num_favorers": 120,
                    "quantity": 15,
                    "price": { "amount": 4990, "divisor": 100, "currency_code": "USD" },
                    "tags": ["dragon lamp", "fire lamp", "fantasy decor"],
                    "materials": ["PLA", "LED"],
                    "images": [{ "url_570xN": "https://img.etsy.com/1.jpg" }],
                    "shop": { "shop_name": "DragonCrafts", "transaction_sold_count": 1500, "review_count": 200, "review_average": 4.9 }
                },
                {
                    "listing_id": 222,
                    "shop_id": 20,
                    "title": "Dragon Lamp Fire Breathing",
                    "description": "Short description.",
                    "views": 100,
                    "num_favorers": 25,
                    "quantity": 5,
                    "price": { "amount": 2990, "divisor": 100, "currency_code": "USD" },
                    "tags": ["dragon lamp"],
                    "materials": ["Resin"],
                    "images": [{ "url_570xN": "https://img.etsy.com/2.jpg" }],
                    "shop": { "shop_name": "FantasyShop", "transaction_sold_count": 300, "review_count": 40, "review_average": 4.5 }
                }
            ]
        }
        """;

        public Task<string> SearchActiveListingsRawJsonAsync(string shopId, string keyword, int limit, CancellationToken ct) =>
            Task.FromResult(RawJson);
    }

    [Fact]
    public async Task SearchMarketAsync_CalculatesEverBeeAndToolsyMetricsCorrectly()
    {
        var fakeClient = new FakeEtsyMarketApiClient();
        var service = new MarketResearchService(fakeClient);

        var query = new MarketSearchQuery(
            Keyword: "dragon lamp",
            Limit: 50,
            SortBy: "market_score",
            ShopId: "53236321"
        );

        var result = await service.SearchMarketAsync(query);

        Assert.Equal("dragon lamp", result.Keyword);
        Assert.Equal(2, result.Listings.Count);

        var first = result.Listings[0];
        Assert.Equal(111, first.Id);
        Assert.Equal(49.90m, first.PriceUsd);
        Assert.True(first.EstimatedMonthlySales > 0);
        Assert.True(first.EstimatedMonthlyRevenue > 0);
        Assert.NotNull(first.SalesVelocity);

        // Check KPIs
        Assert.Equal(2, result.Kpis.TotalListings);
        Assert.True(result.Kpis.AveragePrice > 0);
        Assert.True(result.Kpis.OpportunityScore > 0);
        Assert.NotEmpty(result.Kpis.TopTags);
    }

    [Fact]
    public async Task SearchMarketAsync_WhenKeywordEmpty_ReturnsEmptyResponse()
    {
        var fakeClient = new FakeEtsyMarketApiClient();
        var service = new MarketResearchService(fakeClient);

        var query = new MarketSearchQuery(Keyword: "");

        var result = await service.SearchMarketAsync(query);

        Assert.Empty(result.Listings);
        Assert.Equal(0, result.Total);
    }
}
