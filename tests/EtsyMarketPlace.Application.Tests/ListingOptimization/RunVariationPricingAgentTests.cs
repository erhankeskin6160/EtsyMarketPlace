using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Features.Listings.Commands.RunVariationPricingAgent;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.SeasonalTrends;
using EtsyMarketPlace.Application.Shipping;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.ListingOptimization;

public sealed class RunVariationPricingAgentTests
{
    [Fact]
    public async Task Agent_WithCompetitorsShippingAndLlm_ClampsBelowBreakEvenAndTiersVariations()
    {
        var marketStub = new StubMarketResearchService(new MarketSearchResponse
        {
            Keyword = "Articulated Snake Toy",
            Total = 4,
            Listings =
            [
                new MarketListingItemDto { Id = 1, Title = "Small Articulated Snake 3D", PriceUsd = 14.50m, ShopName = "ShopA", ShopSales = 400, HasFreeShipping = true, EstimatedMonthlySales = 22 },
                new MarketListingItemDto { Id = 2, Title = "Flexible 3D Snake Toy", PriceUsd = 22.00m, ShopName = "ShopB", ShopSales = 900, HasFreeShipping = true, EstimatedMonthlySales = 45, IsBestseller = true },
                new MarketListingItemDto { Id = 3, Title = "Giant Crystal Snake Figurine", PriceUsd = 34.00m, ShopName = "ShopC", ShopSales = 320, HasFreeShipping = false, EstimatedMonthlySales = 18 },
                new MarketListingItemDto { Id = 4, Title = "Rainbow Articulated Snake", PriceUsd = 26.50m, ShopName = "ShopD", ShopSales = 610, HasFreeShipping = true, EstimatedMonthlySales = 30 }
            ]
        });

        var shippingStub = new StubShipmentOrchestratorService();
        // LLM intentionally returns an unsafe low price ($6.00) for Small + tiered prices for Medium/Large
        var llmStub = new StubGeminiListingApiClient("""
            {
              "strategy_summary": "Giriş varyasyonu yüksek CTR için rekabetçi tutuldu, büyük boylarda artan desi korundu.",
              "variations": [
                { "key": "Small (15 cm) / Mat Siyah", "price_usd": 6.00, "tier_label": "Giriş / CTR Fiyatı", "reasoning": "Düşük giriş fiyatı" },
                { "key": "Large (45 cm) / Gökkuşağı / Rainbow", "price_usd": 36.90, "tier_label": "Büyük Boy / Yüksek Marj", "reasoning": "Yüksek desi ve özel filament" }
              ]
            }
            """);

        var handler = new RunVariationPricingAgentHandler(marketStub, shippingStub, llmStub);
        var req = new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Articulated Snake Toy | 3D Printed Flexible Jointed Snake",
            Category: "Toys & Games > Toys",
            ExistingVariations:
            [
                new("Small (15 cm) / Mat Siyah", 0m, 5, true),
                new("Large (45 cm) / Gökkuşağı / Rainbow", 0m, 5, true)
            ],
            TargetCountryCode: "US",
            BaseWeightGrams: 250,
            ProductionCostUsd: 2.00m,
            TargetNetMarginPercent: 30m,
            IncludeOffsiteAds: false,
            UsdTryRate: 50.0m,
            Provider: "DeepSeek",
            Model: "deepseek-chat",
            ApiKey: "sk-test-agent-key");

        var resp = await handler.Handle(new RunVariationPricingAgentCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var result = Assert.IsType<VariationPricingAgentResultDto>(resp.Data);
        Assert.True(result.UsedAi);
        Assert.Equal(4, result.MarketBenchmark.CompetitorCount);
        Assert.Equal("Navlungo", result.ShippingBenchmark.SelectedCarrierName);
        Assert.Equal(4, result.AgentSteps.Count);
        Assert.Equal(2, result.PricedVariations.Count);

        var small = result.PricedVariations.Single(v => v.Key.StartsWith("Small"));
        var large = result.PricedVariations.Single(v => v.Key.StartsWith("Large"));

        // $6.00 from LLM MUST be clamped strictly above break-even so net profit is positive!
        Assert.True(small.RecommendedPriceUsd > small.BreakEvenPriceUsd);
        Assert.True(small.NetProfitUsd > 0m);
        Assert.True(large.RecommendedPriceUsd >= 36.90m);
        Assert.True(large.EstimatedWeightGrams > small.EstimatedWeightGrams);
        Assert.True(large.RecommendedPriceUsd > small.RecommendedPriceUsd);
        Assert.Equal(small.RecommendedPriceUsd, result.RecommendedBasePriceUsd);
    }

    [Fact]
    public async Task Agent_WithoutApiKey_AndEmptyMarket_RunsSingleProductModeAndPreventsLoss()
    {
        var marketStub = new StubMarketResearchService(new MarketSearchResponse
        {
            Keyword = "Ceramic Mug",
            Total = 0,
            Listings = []
        });
        var shippingStub = new StubShipmentOrchestratorService();
        var llmStub = new StubGeminiListingApiClient(string.Empty);

        var handler = new RunVariationPricingAgentHandler(marketStub, shippingStub, llmStub);
        var req = new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Handmade Ceramic Coffee Mug",
            BaseWeightGrams: 400,
            ProductionCostUsd: 3.00m,
            TargetNetMarginPercent: 35m,
            IncludeOffsiteAds: true,
            UsdTryRate: 50.0m);

        var resp = await handler.Handle(new RunVariationPricingAgentCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var result = Assert.IsType<VariationPricingAgentResultDto>(resp.Data);
        Assert.False(result.UsedAi);
        Assert.True(result.IsSingleProductPricing);
        Assert.Equal(0, result.MarketBenchmark.CompetitorCount);
        Assert.Empty(result.PricedVariations);
        Assert.True(string.IsNullOrWhiteSpace(result.SuggestedVarType1));
        Assert.True(string.IsNullOrWhiteSpace(result.SuggestedVarValues1));
        Assert.False(result.SuggestedEnableVar2);
        Assert.True(result.RecommendedBasePriceUsd > result.RecommendedBreakEvenPriceUsd);
        Assert.True(result.RecommendedNetProfitUsd > 0m);
    }

    [Fact]
    public async Task Agent_WithEnableVariationsFalse_IgnoresStaleVariationInputsAndReturnsSingleProductPrice()
    {
        var marketStub = new StubMarketResearchService(new MarketSearchResponse
        {
            Keyword = "Thinking Alien",
            Total = 3,
            Listings =
            [
                new MarketListingItemDto { Id = 1, Title = "Alien Figurine A", PriceUsd = 24.00m, HasFreeShipping = true },
                new MarketListingItemDto { Id = 2, Title = "Alien Figurine B", PriceUsd = 29.00m, HasFreeShipping = true },
                new MarketListingItemDto { Id = 3, Title = "Alien Figurine C", PriceUsd = 35.00m, HasFreeShipping = false }
            ]
        });
        var shippingStub = new StubShipmentOrchestratorService();
        var llmStub = new StubGeminiListingApiClient("""
            {
              "strategy_summary": "Tekil ürün için kargo ve Etsy kesintileri düşülerek %30 net marjlı tek fiyat belirlendi.",
              "price_usd": 29.90,
              "tier_label": "Tekil Ürün Hedef Marj",
              "reasoning": "Kargo ve kesintiler sonrası güvenli net kâr sağlar."
            }
            """);

        var handler = new RunVariationPricingAgentHandler(marketStub, shippingStub, llmStub);
        var req = new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Thinking Alien Desk Figurine",
            VariationType1: "Boyut / Size",
            VariationValues1: "Small, Medium, Large",
            EnableVariation2: true,
            VariationType2: "Renk / Color",
            VariationValues2: "Siyah, Beyaz",
            ExistingVariations: [new("Small / Siyah", 19.90m, 5, true)],
            BaseWeightGrams: 250,
            ProductionCostUsd: 2.00m,
            TargetNetMarginPercent: 30m,
            IncludeOffsiteAds: false,
            UsdTryRate: 50.0m,
            Provider: "DeepSeek",
            Model: "deepseek-chat",
            ApiKey: "sk-test-key",
            EnableVariations: false);

        var resp = await handler.Handle(new RunVariationPricingAgentCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var result = Assert.IsType<VariationPricingAgentResultDto>(resp.Data);
        Assert.True(result.UsedAi);
        Assert.True(result.IsSingleProductPricing);
        Assert.Empty(result.PricedVariations);
        Assert.True(string.IsNullOrWhiteSpace(result.SuggestedVarType1));
        Assert.True(string.IsNullOrWhiteSpace(result.SuggestedVarValues1));
        Assert.False(result.SuggestedEnableVar2);
        Assert.True(result.RecommendedBasePriceUsd >= 29.90m);
        Assert.True(result.RecommendedBasePriceUsd > result.RecommendedBreakEvenPriceUsd);
        Assert.True(result.RecommendedNetProfitUsd > 0m);
    }

    [Fact]
    public async Task Agent_WithEnableVariationsFalse_IgnoresLlmVariationsArrayAndStaysSingleProduct()
    {
        var marketStub = new StubMarketResearchService(new MarketSearchResponse
        {
            Keyword = "Wooden Bowl",
            Total = 0,
            Listings = []
        });
        var shippingStub = new StubShipmentOrchestratorService();
        var llmStub = new StubGeminiListingApiClient("""
            {
              "strategy_summary": "Tekil ürün fiyatı belirlendi.",
              "variations": [
                { "key": "Small", "price_usd": 34.90, "tier_label": "Tekil", "reasoning": "Güvenli marj" }
              ]
            }
            """);

        var handler = new RunVariationPricingAgentHandler(marketStub, shippingStub, llmStub);
        var req = new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Handcrafted Olive Wood Serving Bowl",
            BaseWeightGrams: 300,
            ProductionCostUsd: 3.50m,
            TargetNetMarginPercent: 30m,
            IncludeOffsiteAds: false,
            UsdTryRate: 50.0m,
            Provider: "OpenAI",
            Model: "gpt-4o-mini",
            ApiKey: "sk-openai-test",
            EnableVariations: false);

        var resp = await handler.Handle(new RunVariationPricingAgentCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var result = Assert.IsType<VariationPricingAgentResultDto>(resp.Data);
        Assert.True(result.IsSingleProductPricing);
        Assert.True(result.UsedAi);
        Assert.Empty(result.PricedVariations);
        Assert.True(string.IsNullOrWhiteSpace(result.SuggestedVarValues1));
        Assert.True(result.RecommendedBasePriceUsd >= 34.90m);
        Assert.True(result.RecommendedBasePriceUsd > result.RecommendedBreakEvenPriceUsd);
    }

    [Fact]
    public async Task Agent_WithEnableVariationsTrue_AndEmptyVariations_RemainsSingleProductWithoutHardcodedFallback()
    {
        var marketStub = new StubMarketResearchService(new MarketSearchResponse
        {
            Keyword = "Candle Holder",
            Total = 0,
            Listings = []
        });
        var shippingStub = new StubShipmentOrchestratorService();
        var llmStub = new StubGeminiListingApiClient("""
            {
              "strategy_summary": "Varyasyonsuz tek fiyat uygulandı.",
              "price_usd": 28.90,
              "tier_label": "Hedef Marj",
              "reasoning": "Güvenli marj"
            }
            """);

        var handler = new RunVariationPricingAgentHandler(marketStub, shippingStub, llmStub);
        var req = new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Minimalist Ceramic Candle Holder",
            BaseWeightGrams: 250,
            ProductionCostUsd: 2.00m,
            TargetNetMarginPercent: 30m,
            UsdTryRate: 50.0m,
            Provider: "Gemini",
            Model: "gemini-2.5-flash",
            ApiKey: "gm-test",
            EnableVariations: true,
            VariationValues1: "",
            ExistingVariations: []);

        var resp = await handler.Handle(new RunVariationPricingAgentCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var result = Assert.IsType<VariationPricingAgentResultDto>(resp.Data);
        Assert.True(result.IsSingleProductPricing);
        Assert.Empty(result.PricedVariations);
        Assert.True(string.IsNullOrWhiteSpace(result.SuggestedVarValues1));
        Assert.False(result.SuggestedEnableVar2);
    }

    [Fact]
    public async Task Agent_WithAllInactiveExistingVariations_DoesNotResurrectFromVariationValues1()
    {
        var marketStub = new StubMarketResearchService(new MarketSearchResponse
        {
            Keyword = "Sculpture",
            Total = 0,
            Listings = []
        });
        var shippingStub = new StubShipmentOrchestratorService();
        var llmStub = new StubGeminiListingApiClient(string.Empty);

        var handler = new RunVariationPricingAgentHandler(marketStub, shippingStub, llmStub);
        var req = new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Abstract Resin Sculpture",
            EnableVariations: true,
            VariationType1: "Size",
            VariationValues1: "Small, Large",
            ExistingVariations:
            [
                new("Small", 25.0m, 5, false),
                new("Large", 35.0m, 5, false)
            ],
            BaseWeightGrams: 300,
            ProductionCostUsd: 2.50m,
            TargetNetMarginPercent: 30m,
            UsdTryRate: 50.0m);

        var resp = await handler.Handle(new RunVariationPricingAgentCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var result = Assert.IsType<VariationPricingAgentResultDto>(resp.Data);
        Assert.True(result.IsSingleProductPricing);
        Assert.Empty(result.PricedVariations);
        Assert.True(string.IsNullOrWhiteSpace(result.SuggestedVarValues1));
    }

    [Fact]
    public async Task Agent_DoesNotFalseMatchLedOrPossessiveSInVariationKeys()
    {
        var marketStub = new StubMarketResearchService(new MarketSearchResponse
        {
            Keyword = "Ceramic Vase",
            Total = 0,
            Listings = []
        });
        var shippingStub = new StubShipmentOrchestratorService();
        var llmStub = new StubGeminiListingApiClient(string.Empty);

        var handler = new RunVariationPricingAgentHandler(marketStub, shippingStub, llmStub);
        var req = new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Ceramic Glazed Vase",
            EnableVariations: true,
            ExistingVariations:
            [
                new("Women's Medium / Speckled White", 0m, 5, true),
                new("Standard Medium / Matte White", 0m, 5, true)
            ],
            BaseWeightGrams: 300,
            ProductionCostUsd: 2.50m,
            TargetNetMarginPercent: 30m,
            UsdTryRate: 50.0m);

        var resp = await handler.Handle(new RunVariationPricingAgentCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var result = Assert.IsType<VariationPricingAgentResultDto>(resp.Data);
        Assert.Equal(2, result.PricedVariations.Count);

        var speckledRow = result.PricedVariations.Single(v => v.Key.StartsWith("Women's"));
        var standardRow = result.PricedVariations.Single(v => v.Key.StartsWith("Standard"));

        // "'s" must not trigger 0.80x Small weight scaling, and "speckled" must not trigger 1.18x LED premium
        Assert.Equal(standardRow.EstimatedWeightGrams, speckledRow.EstimatedWeightGrams);
        Assert.Equal(standardRow.RecommendedPriceUsd, speckledRow.RecommendedPriceUsd);
    }

    private sealed class StubMarketResearchService(MarketSearchResponse searchResult) : IMarketResearchService
    {
        public Task<MarketSearchResponse> SearchMarketAsync(MarketSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(searchResult);

        public Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(MarketAiReportCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CompetitorShopAnalysisDto> AnalyzeCompetitorShopAsync(string shopNameOrUrl, int limit = 50, string authShopId = "53236321", CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IReadOnlyList<SeasonalTrendItem> GetSeasonalTrends(DateTime? referenceDate = null)
            => [];
    }

    private sealed class StubShipmentOrchestratorService : IShipmentOrchestratorService
    {
        public Task<CarrierQuotesResponseDto> GetCarrierQuotesAsync(CarrierQuotesQuery query, CancellationToken cancellationToken = default)
        {
            var weightKg = Math.Max(0.1, query.WeightKg);
            decimal navPrice = Math.Round((decimal)(8.50 + weightKg * 4.5), 2);
            decimal sePrice = Math.Round((decimal)(10.20 + weightKg * 5.0), 2);

            return Task.FromResult(new CarrierQuotesResponseDto
            {
                CalculatedDesi = Math.Round((query.LengthCm * query.WidthCm * query.HeightCm) / 5000.0, 2),
                BillableWeightKg = Math.Max(weightKg, Math.Round((query.LengthCm * query.WidthCm * query.HeightCm) / 5000.0, 2)),
                Quotes =
                [
                    new UnifiedCarrierQuoteDto
                    {
                        CarrierKey = "navlungo",
                        CarrierName = "Navlungo",
                        ServiceType = "Widect — Navlungo",
                        PriceUsd = navPrice,
                        PriceTry = Math.Round(navPrice * query.UsdTryRate, 2),
                        EstimatedDays = "3-5 İş Günü",
                        IsLowestPrice = true
                    },
                    new UnifiedCarrierQuoteDto
                    {
                        CarrierKey = "shipentegra",
                        CarrierName = "ShipEntegra",
                        ServiceType = "Eco Express",
                        PriceUsd = sePrice,
                        PriceTry = Math.Round(sePrice * query.UsdTryRate, 2),
                        EstimatedDays = "3-6 İş Günü"
                    }
                ]
            });
        }

        public IReadOnlyList<HsCodeSearchItemDto> SearchHsCodes(string? query, int limit = 25)
            => [new HsCodeSearchItemDto { Code = "9503007000", Category = "Oyuncak", Description = "Eklemli Oyuncak" }];

        public Task<CreateCarrierShipmentResult> CreateShipmentAsync(CreateCarrierShipmentCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ConnectCarrierResult> ConnectCarrierAsync(ConnectCarrierCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<CarrierSessionStatusDto>> GetCarrierSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CarrierSessionStatusDto>>([]);
    }

    private sealed class StubGeminiListingApiClient(string jsonResponse) : IGeminiListingApiClient
    {
        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction, string userPrompt, string model, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult((!string.IsNullOrWhiteSpace(jsonResponse), (string?)jsonResponse, (string?)null));

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
            string systemInstruction, string userPrompt, IReadOnlyList<VisionImageInput> images, string model, string apiKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
