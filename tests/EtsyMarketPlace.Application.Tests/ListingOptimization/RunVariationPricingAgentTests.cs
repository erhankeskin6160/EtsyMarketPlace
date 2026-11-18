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
    public async Task Agent_WithoutApiKey_AndEmptyMarket_SuggestsDefaultVariationsAndPreventsLoss()
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
        Assert.Equal(0, result.MarketBenchmark.CompetitorCount);
        Assert.NotEmpty(result.PricedVariations);
        Assert.False(string.IsNullOrWhiteSpace(result.SuggestedVarType1));
        Assert.All(result.PricedVariations, v =>
        {
            Assert.True(v.RecommendedPriceUsd > v.BreakEvenPriceUsd);
            Assert.True(v.NetProfitUsd > 0m);
        });
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
