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

        public string? ShopByNameRawJson { get; set; }
        public string? ShopByIdRawJson { get; set; }
        public string? ShopActiveListingsRawJson { get; set; }

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

        public Task<string> GetShopByNameRawJsonAsync(string authShopId, string targetShopName, CancellationToken ct)
        {
            return Task.FromResult(ShopByNameRawJson ?? """{ "count": 0, "results": [] }""");
        }

        public Task<string> GetShopByIdRawJsonAsync(string authShopId, long targetShopId, CancellationToken ct)
        {
            return Task.FromResult(ShopByIdRawJson ?? """{}""");
        }

        public Task<string> GetShopActiveListingsRawJsonAsync(string authShopId, long targetShopId, int limit, CancellationToken ct)
        {
            return Task.FromResult(ShopActiveListingsRawJson ?? """{ "count": 0, "results": [] }""");
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

    [Fact]
    public async Task SearchMarketAsync_RecalculatesSalesAndScoresAfterBatchEnrichment()
    {
        var unenrichedClient = new FakeEtsyMarketApiClient
        {
            RawJson = MixedCurrencySearchJson,
            BatchRawJson = """{ "results": [] }"""
        };
        var enrichedClient = new FakeEtsyMarketApiClient
        {
            RawJson = MixedCurrencySearchJson,
            BatchRawJson = """
            {
                "results": [
                    {
                        "listing_id": 111,
                        "url": "https://www.etsy.com/listing/111/dragon-lamp",
                        "tags": ["t1","t2","t3","t4","t5","t6","t7","t8","t9","t10","t11","t12","t13"],
                        "images": [
                            { "url_570xN": "https://i.etsystatic.com/img1.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/img2.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/img3.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/img4.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/img5.jpg" }
                        ],
                        "shop": {
                            "shop_name": "DragonCrafts",
                            "url": "https://www.etsy.com/shop/DragonCrafts",
                            "transaction_sold_count": 8500,
                            "review_count": 1200,
                            "review_average": 4.95
                        }
                    }
                ]
            }
            """
        };

        var unenrichedRes = await new MarketResearchService(unenrichedClient).SearchMarketAsync(
            new MarketSearchQuery(Keyword: "recalc probe unenriched", Limit: 25, ShopId: "53236321"));
        var enrichedRes = await new MarketResearchService(enrichedClient).SearchMarketAsync(
            new MarketSearchQuery(Keyword: "recalc probe enriched", Limit: 25, ShopId: "53236321"));

        var itemUnenriched = unenrichedRes.Listings.Single(l => l.Id == 111);
        var itemEnriched = enrichedRes.Listings.Single(l => l.Id == 111);

        Assert.True(itemEnriched.SeoScore > itemUnenriched.SeoScore);
        Assert.True(itemEnriched.MarketScore > itemUnenriched.MarketScore);
        Assert.True(itemEnriched.EstimatedMonthlySales > itemUnenriched.EstimatedMonthlySales);
        Assert.True(itemEnriched.EstimatedMonthlyRevenue > itemUnenriched.EstimatedMonthlyRevenue);
        Assert.Equal(49.90m, enrichedRes.Kpis.MedianPrice);
        Assert.Equal(3, enrichedRes.Kpis.UniqueShopCount);
    }

    private sealed class FakeGeminiClient : EtsyMarketPlace.Application.ListingOptimization.IGeminiListingApiClient
    {
        public string? ResponseJson { get; set; }

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction,
            string userPrompt,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(ResponseJson))
            {
                return Task.FromResult<(bool, string?, string?)>((false, null, "Simulated failure"));
            }
            return Task.FromResult<(bool, string?, string?)>((true, ResponseJson, null));
        }
    }

    [Fact]
    public async Task GenerateAiMarketReportAsync_WithGeminiResponse_ReturnsAiStrategyReport()
    {
        var fakeMarketClient = new FakeEtsyMarketApiClient();
        var fakeGemini = new FakeGeminiClient
        {
            ResponseJson = """
            {
                "market_overview": "Dragon lamp nişinde yüksek talep ve orta rekabet var.",
                "pricing_strategy": "$39.90 ile giriş yapın.",
                "differentiation_ideas": ["RGB kumandalı taban ekleyin", "Hediye kutusu sunun"],
                "recommended_keywords": ["dragon night light", "resin dragon lamp"],
                "action_items": ["13 etiketi tamamlayın", "Video ekleyin"]
            }
            """
        };
        var service = new MarketResearchService(fakeMarketClient, fakeGemini);

        var report = await service.GenerateAiMarketReportAsync(new MarketAiReportCommand(
            Keyword: "dragon lamp",
            AveragePrice: 44.50m,
            MinPrice: 19.90m,
            MaxPrice: 89.00m,
            MedianPrice: 42.00m,
            OpportunityScore: 84,
            TotalEstimatedMarketRevenue: 18500m,
            UniqueShopCount: 18,
            TotalListings: 25,
            ApiKey: "test-gemini-key",
            Model: "gemini-2.5-flash"
        ));

        Assert.True(report.UsedAi);
        Assert.Equal("Gemini", report.Provider);
        Assert.Contains("yüksek talep", report.MarketOverview);
        Assert.Equal(2, report.DifferentiationIdeas.Count);
        Assert.Equal(2, report.RecommendedKeywords.Count);
        Assert.Contains("dragon night light", report.ReportHtml);
    }

    [Fact]
    public async Task GenerateAiMarketReportAsync_WithoutApiKey_ReturnsDeterministicRealDataReport()
    {
        var fakeMarketClient = new FakeEtsyMarketApiClient();
        var service = new MarketResearchService(fakeMarketClient);

        var report = await service.GenerateAiMarketReportAsync(new MarketAiReportCommand(
            Keyword: "wooden wall art",
            AveragePrice: 55.00m,
            MinPrice: 25.00m,
            MaxPrice: 120.00m,
            MedianPrice: 50.00m,
            OpportunityScore: 78,
            TotalEstimatedMarketRevenue: 24000m,
            UniqueShopCount: 20,
            TotalListings: 25,
            TopShopName: "WoodCraftStudio",
            TopShopSales: 3400,
            TopTags: ["wall decor", "wood art", "housewarming gift"]
        ));

        Assert.False(report.UsedAi);
        Assert.Equal("SmartMarketEngine", report.Provider);
        Assert.Contains("wooden wall art", report.MarketOverview);
        Assert.NotEmpty(report.DifferentiationIdeas);
        Assert.NotEmpty(report.RecommendedKeywords);
        Assert.NotEmpty(report.ActionItems);
        Assert.Contains("WoodCraftStudio", report.ReportHtml);
    }

    [Fact]
    public async Task AnalyzeCompetitorShopAsync_ParsesUrlAndCalculatesEverBeeAndLqsMetrics()
    {
        var fakeMarketClient = new FakeEtsyMarketApiClient
        {
            ShopByNameRawJson = """
            {
                "count": 1,
                "results": [
                    {
                        "shop_id": 998877,
                        "shop_name": "ArtisanWoodDecor",
                        "title": "Handcrafted Wooden Wall Art & Lamps",
                        "url": "https://www.etsy.com/shop/ArtisanWoodDecor",
                        "icon_url_fullxfull": "https://i.etsystatic.com/shop_icon.jpg",
                        "transaction_sold_count": 3600,
                        "listing_active_count": 42,
                        "review_average": 4.92,
                        "review_count": 680,
                        "create_date": 1680000000
                    }
                ]
            }
            """,
            ShopActiveListingsRawJson = """
            {
                "count": 2,
                "results": [
                    {
                        "listing_id": 5001,
                        "title": "Personalized Wooden Wall Art Geometric Mountain Decor Large Living Room Gift",
                        "description": "Handcrafted wooden mountain wall art made from sustainable walnut and oak wood. Ready to hang with hardware included. Perfect housewarming gift for nature lovers and modern rustic homes.",
                        "views": 1800,
                        "num_favorers": 340,
                        "quantity": 10,
                        "price": { "amount": 8500, "divisor": 100, "currency_code": "USD" },
                        "tags": ["wood wall art", "mountain decor", "housewarming gift", "rustic wall art", "walnut wood", "living room art", "geometric wood", "nature wall decor", "custom wood sign", "modern cabin", "anniversary gift", "large wall art", "handmade decor"],
                        "images": [
                            { "url_570xN": "https://i.etsystatic.com/l1.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/l2.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/l3.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/l4.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/l5.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/l6.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/l7.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/l8.jpg" }
                        ],
                        "url": "https://www.etsy.com/listing/5001"
                    },
                    {
                        "listing_id": 5002,
                        "title": "Small Wood Coaster",
                        "description": "Simple coaster.",
                        "views": 90,
                        "num_favorers": 12,
                        "quantity": 4,
                        "price": { "amount": 1900, "divisor": 100, "currency_code": "USD" },
                        "tags": ["wood coaster", "housewarming gift"],
                        "images": [
                            { "url_570xN": "https://i.etsystatic.com/l9.jpg" }
                        ],
                        "url": "https://www.etsy.com/listing/5002"
                    }
                ]
            }
            """
        };

        var service = new MarketResearchService(fakeMarketClient);
        var result = await service.AnalyzeCompetitorShopAsync(
            shopNameOrUrl: "https://www.etsy.com/shop/ArtisanWoodDecor?ref=shop-header-name",
            limit: 50,
            authShopId: "53236321"
        );

        Assert.True(result.Found);
        Assert.Equal(998877, result.Shop.ShopId);
        Assert.Equal("ArtisanWoodDecor", result.Shop.ShopName);
        Assert.Equal(3600, result.Shop.TotalSalesCount);
        Assert.True(result.Shop.EstimatedMonthlyRevenueUsd > 0);
        Assert.True(result.Shop.CompetitorStrengthScore > 0);
        Assert.Equal(2, result.Listings.Count);

        var topItem = result.Listings[0];
        Assert.Equal(5001, topItem.ListingId);
        Assert.Equal(85.00m, topItem.PriceUsd);
        Assert.True(topItem.EstimatedMonthlyRevenueUsd > result.Listings[1].EstimatedMonthlyRevenueUsd);
        Assert.StartsWith("A", topItem.LqsGrade);

        Assert.NotEmpty(result.TopTags);
        Assert.Contains(result.TopTags, t => t.Term == "housewarming gift" && t.Count == 2);
        Assert.NotEmpty(result.TopTitleWords);
        Assert.Contains("ArtisanWoodDecor", result.AiCompetitiveGapInsight, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSeasonalTrends_ReturnsSortedYearlyEvents()
    {
        var fakeMarketClient = new FakeEtsyMarketApiClient();
        var service = new MarketResearchService(fakeMarketClient);

        var trends = service.GetSeasonalTrends();

        Assert.NotNull(trends);
        Assert.True(trends.Count >= 6);
        Assert.All(trends, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.EventName));
            Assert.NotEmpty(t.HighDemandKeywords);
            Assert.NotEmpty(t.WinningTags);
        });
    }

    [Fact]
    public async Task AnalyzeCompetitorShopAsync_EnrichesListingsWithBatchImagesAndMaterials()
    {
        var fakeMarketClient = new FakeEtsyMarketApiClient
        {
            ShopByNameRawJson = """
            {
                "count": 1,
                "results": [
                    {
                        "shop_id": 445566,
                        "shop_name": "LampCraftStudio",
                        "title": "Modern Wood & Resin Lamps",
                        "url": "https://www.etsy.com/shop/LampCraftStudio",
                        "transaction_sold_count": 1200,
                        "listing_active_count": 15,
                        "review_average": 4.9,
                        "review_count": 210,
                        "create_date": 1680000000
                    }
                ]
            }
            """,
            ShopActiveListingsRawJson = """
            {
                "count": 1,
                "results": [
                    {
                        "listing_id": 7001,
                        "title": "Custom Resin Night Light Wooden Base Lamp",
                        "views": 640,
                        "num_favorers": 95,
                        "quantity": 5,
                        "price": { "amount": 4990, "divisor": 100, "currency_code": "USD" },
                        "tags": ["resin lamp", "night light", "wood lamp"]
                    }
                ]
            }
            """,
            BatchRawJson = """
            {
                "results": [
                    {
                        "listing_id": 7001,
                        "url": "https://www.etsy.com/listing/7001/custom-resin-night-light",
                        "description": "Full detailed description from batch endpoint.",
                        "materials": ["Walnut Wood", "Epoxy Resin", "LED Strip"],
                        "Images": [
                            { "url_fullxfull": "https://i.etsystatic.com/batch_full_1.jpg", "url_570xN": "https://i.etsystatic.com/batch_570_1.jpg" },
                            { "url_fullxfull": "https://i.etsystatic.com/batch_full_2.jpg" },
                            { "url_570xN": "https://i.etsystatic.com/batch_570_3.jpg" }
                        ]
                    }
                ]
            }
            """
        };

        var service = new MarketResearchService(fakeMarketClient);
        var result = await service.AnalyzeCompetitorShopAsync(
            shopNameOrUrl: "LampCraftStudio",
            limit: 25,
            authShopId: "53236321"
        );

        Assert.True(result.Found);
        Assert.Equal(1, fakeMarketClient.BatchCallCount);
        var item = Assert.Single(result.Listings);
        Assert.Equal(7001, item.ListingId);
        Assert.Equal("https://i.etsystatic.com/batch_full_1.jpg", item.ImageUrl);
        Assert.Equal(3, item.ImageUrls.Count);
        Assert.Equal("https://www.etsy.com/listing/7001/custom-resin-night-light", item.ListingUrl);
        Assert.Equal("Full detailed description from batch endpoint.", item.Description);
        Assert.Equal(3, item.Materials.Count);
        Assert.Contains("Epoxy Resin", item.Materials);
    }
}

