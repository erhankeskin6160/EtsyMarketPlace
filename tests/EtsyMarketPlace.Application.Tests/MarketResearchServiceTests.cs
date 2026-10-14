namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.MarketResearch;
using Xunit;

public sealed class MarketResearchServiceTests
{
    private sealed class FakeEtsyMarketApiClient : IEtsyMarketApiClient
    {
        public string RawJson { get; set; } = """{ "count": 0, "results": [] }""";
        public string? BatchRawJson { get; set; }
        public bool ThrowOnBatch { get; set; }
        public int SearchCallCount { get; private set; }
        public int BatchCallCount { get; private set; }
        public List<long> LastBatchIds { get; } = new();

        public Task<string> SearchActiveListingsRawJsonAsync(string shopId, string keyword, int limit, CancellationToken ct)
        {
            SearchCallCount++;
            return Task.FromResult(RawJson);
        }

        public Task<string> GetListingsBatchRawJsonAsync(string shopId, IReadOnlyList<long> listingIds, CancellationToken ct)
        {
            BatchCallCount++;
            LastBatchIds.Clear();
            LastBatchIds.AddRange(listingIds);

            if (ThrowOnBatch)
            {
                throw new InvalidOperationException("batch hatası (test)");
            }

            return Task.FromResult(BatchRawJson ?? """{ "results": [] }""");
        }
    }

    private const string TwoItemSearchJson = """
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

    private const string MixedCurrencySearchJson = """
    {
        "count": 3,
        "results": [
            {
                "listing_id": 111,
                "shop_id": 10,
                "title": "Dragon Lamp 3D Printed Night Light Artisan Decor Fantasy",
                "description": "Açıklama metni.",
                "views": 500,
                "num_favorers": 120,
                "quantity": 15,
                "price": { "amount": 4990, "divisor": 100, "currency_code": "USD" },
                "tags": ["dragon lamp", "fire lamp", "fantasy decor"],
                "url": "https://www.etsy.com/listing/111"
            },
            {
                "listing_id": 222,
                "shop_id": 20,
                "title": "Dragon Lamp Fire Breathing",
                "description": "Açıklama metni.",
                "views": 100,
                "num_favorers": 25,
                "quantity": 5,
                "price": { "amount": 10000, "divisor": 100, "currency_code": "CAD" },
                "tags": ["dragon lamp"],
                "url": "https://www.etsy.com/listing/222"
            },
            {
                "listing_id": 333,
                "shop_id": 30,
                "title": "Dragon Lamp Rupiah Edition",
                "description": "Açıklama metni.",
                "views": 50,
                "num_favorers": 5,
                "quantity": 3,
                "price": { "amount": 388303, "divisor": 1, "currency_code": "IDR" },
                "tags": ["dragon lamp"]
            }
        ]
    }
    """;

    private const string EnrichBatchJson = """
    {
        "results": [
            {
                "listing_id": 111,
                "url": "https://www.etsy.com/listing/111/dragon-lamp",
                "images": [
                    { "url_570xN": "https://i.etsystatic.com/img1.jpg" },
                    { "url_570xN": "https://i.etsystatic.com/img2.jpg" }
                ],
                "shop": {
                    "shop_name": "DragonCrafts",
                    "url": "https://www.etsy.com/shop/DragonCrafts",
                    "transaction_sold_count": 1500,
                    "review_count": 200,
                    "review_average": 4.9
                }
            }
        ]
    }
    """;

    [Fact]
    public async Task SearchMarketAsync_CalculatesEverBeeAndToolsyMetricsCorrectly()
    {
        var fakeClient = new FakeEtsyMarketApiClient { RawJson = TwoItemSearchJson };
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
        Assert.Equal("DragonCrafts", first.ShopName);
        Assert.True(first.EstimatedMonthlySales > 0);
        Assert.True(first.EstimatedMonthlyRevenue > 0);
        Assert.NotNull(first.SalesVelocity);

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
        Assert.Equal(0, fakeClient.SearchCallCount);
    }

    [Fact]
    public async Task SearchMarketAsync_ConvertsMixedCurrenciesToUsd()
    {
        var fakeClient = new FakeEtsyMarketApiClient { RawJson = MixedCurrencySearchJson };
        var service = new MarketResearchService(fakeClient);

        var result = await service.SearchMarketAsync(new MarketSearchQuery(
            Keyword: "mixed currency probe",
            Limit: 25,
            SortBy: "market_score",
            ShopId: "53236321"
        ));

        Assert.Equal(3, result.Listings.Count);
        Assert.Equal(49.90m, result.Listings.Single(l => l.Id == 111).PriceUsd);
        Assert.Equal(72.00m, result.Listings.Single(l => l.Id == 222).PriceUsd);   // CAD × 0.72
        Assert.Equal(23.88m, result.Listings.Single(l => l.Id == 333).PriceUsd);   // IDR × 0.0000615

        Assert.Equal("USD", result.Kpis.Currency);
        Assert.Equal(48.59m, result.Kpis.AveragePrice);  // (49.90 + 72.00 + 23.88) / 3
        Assert.Equal(23.88m, result.Kpis.MinPrice);
        Assert.Equal(72.00m, result.Kpis.MaxPrice);
    }

    [Fact]
    public async Task SearchMarketAsync_EnrichesWithImagesAndShopDetails()
    {
        var fakeClient = new FakeEtsyMarketApiClient
        {
            RawJson = MixedCurrencySearchJson,
            BatchRawJson = EnrichBatchJson
        };
        var service = new MarketResearchService(fakeClient);

        var result = await service.SearchMarketAsync(new MarketSearchQuery(
            Keyword: "enrich probe",
            Limit: 25,
            SortBy: "market_score",
            ShopId: "53236321"
        ));

        var enriched = result.Listings.Single(l => l.Id == 111);
        Assert.Equal("DragonCrafts", enriched.ShopName);
        Assert.Equal("https://www.etsy.com/shop/DragonCrafts", enriched.ShopUrl);
        Assert.Equal(1500, enriched.ShopSales);
        Assert.Equal(200, enriched.ReviewCount);
        Assert.Equal(4.9m, enriched.ReviewAverage);
        Assert.Equal("https://i.etsystatic.com/img1.jpg", enriched.ImageUrl);
        Assert.Equal(2, enriched.ImageUrls.Count);
        Assert.Equal("https://www.etsy.com/listing/111/dragon-lamp", enriched.ListingUrl);

        Assert.Equal("DragonCrafts", result.Kpis.TopShopName);
        Assert.Equal(1500, result.Kpis.TopShopSales);

        Assert.Equal(1, fakeClient.BatchCallCount);
        Assert.Equal(3, fakeClient.LastBatchIds.Count);
        Assert.Contains(111L, fakeClient.LastBatchIds);
    }

    [Fact]
    public async Task SearchMarketAsync_EnrichmentFailure_StillReturnsSearchResults()
    {
        var fakeClient = new FakeEtsyMarketApiClient
        {
            RawJson = MixedCurrencySearchJson,
            ThrowOnBatch = true
        };
        var service = new MarketResearchService(fakeClient);

        var result = await service.SearchMarketAsync(new MarketSearchQuery(
            Keyword: "enrich fail probe",
            Limit: 25,
            SortBy: "market_score",
            ShopId: "53236321"
        ));

        Assert.Equal(3, result.Listings.Count);
        Assert.Equal("Shop 10", result.Listings.Single(l => l.Id == 111).ShopName);
        Assert.Equal(1, fakeClient.BatchCallCount);
    }

    [Fact]
    public async Task SearchMarketAsync_IdenticalSearch_ServedFromCache()
    {
        var fakeClient = new FakeEtsyMarketApiClient { RawJson = MixedCurrencySearchJson };
        var service = new MarketResearchService(fakeClient);
        var query = new MarketSearchQuery(
            Keyword: "cache probe identical",
            Limit: 25,
            SortBy: "market_score",
            ShopId: "53236321"
        );

        var first = await service.SearchMarketAsync(query);
        var second = await service.SearchMarketAsync(query);

        Assert.Same(first, second);
        Assert.Equal(1, fakeClient.SearchCallCount);
        Assert.Equal(1, fakeClient.BatchCallCount);
    }

    [Fact]
    public async Task SearchMarketAsync_DifferentKeywords_NotCachedTogether()
    {
        var fakeClient = new FakeEtsyMarketApiClient { RawJson = """{ "count": 0, "results": [] }""" };
        var service = new MarketResearchService(fakeClient);

        await service.SearchMarketAsync(new MarketSearchQuery(Keyword: "cache probe alpha", Limit: 25, ShopId: "53236321"));
        await service.SearchMarketAsync(new MarketSearchQuery(Keyword: "cache probe beta", Limit: 25, ShopId: "53236321"));

        Assert.Equal(2, fakeClient.SearchCallCount);
    }
}
