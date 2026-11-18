using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Features.Listings.Commands.RunVariationPricingAgent;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.SeasonalTrends;
using EtsyMarketPlace.Application.Shipping;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public sealed class RunVariationPricingAgentHandlerTests
{
    [Fact]
    public async Task Handle_FallsBackToTagKeyword_WhenPrimarySearchReturnsEmpty()
    {
        var marketService = new StubMarketResearchService(query =>
        {
            if (query.Keyword.Equals("dragon desk lamp", StringComparison.OrdinalIgnoreCase))
            {
                return new MarketSearchResponse
                {
                    Listings =
                    [
                        new MarketListingItemDto { Id = 1, Title = "Dragon Lamp A", PriceUsd = 45m, HasFreeShipping = true },
                        new MarketListingItemDto { Id = 2, Title = "Dragon Lamp B", PriceUsd = 55m, HasFreeShipping = false },
                        new MarketListingItemDto { Id = 3, Title = "Dragon Lamp C", PriceUsd = 65m, HasFreeShipping = true }
                    ]
                };
            }

            return new MarketSearchResponse { Listings = [] };
        });

        var handler = new RunVariationPricingAgentHandler(
            marketService,
            new StubShipmentOrchestratorService(),
            new StubGeminiClient());

        var result = await handler.Handle(new RunVariationPricingAgentCommand(new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Mystic Nocturne Ember Wyrm Sculpted Glow Piece",
            Tags: ["dragon desk lamp", "fantasy decor"],
            BaseWeightGrams: 250,
            ProductionCostUsd: 6m,
            TargetNetMarginPercent: 30m
        )), CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<VariationPricingAgentResultDto>(result.Data);
        Assert.Equal("dragon desk lamp", data.SearchKeywordUsed);
        Assert.True(data.MarketBenchmark.UsedLiveMarketData);
        Assert.Equal(3, data.MarketBenchmark.CompetitorCount);
        Assert.Equal(55m, data.MarketBenchmark.MedianPriceUsd);
    }

    [Fact]
    public async Task Handle_FiltersExtremeLowOutliers_AndSplitsShippingMedians()
    {
        var marketService = new StubMarketResearchService(_ => new MarketSearchResponse
        {
            Listings =
            [
                new MarketListingItemDto { Id = 1, Title = "Keychain Addon", PriceUsd = 5m, HasFreeShipping = false },
                new MarketListingItemDto { Id = 2, Title = "Articulated Dragon S", PriceUsd = 36m, HasFreeShipping = false },
                new MarketListingItemDto { Id = 3, Title = "Articulated Dragon M", PriceUsd = 44m, HasFreeShipping = false },
                new MarketListingItemDto { Id = 4, Title = "Articulated Dragon L", PriceUsd = 54m, HasFreeShipping = true },
                new MarketListingItemDto { Id = 5, Title = "Articulated Dragon XL", PriceUsd = 64m, HasFreeShipping = true }
            ]
        });

        var handler = new RunVariationPricingAgentHandler(
            marketService,
            new StubShipmentOrchestratorService(),
            new StubGeminiClient());

        var result = await handler.Handle(new RunVariationPricingAgentCommand(new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Articulated Crystal Dragon Figurine",
            BaseWeightGrams: 250,
            ProductionCostUsd: 5m,
            TargetNetMarginPercent: 30m
        )), CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<VariationPricingAgentResultDto>(result.Data);
        Assert.Equal(1, data.MarketBenchmark.FilteredOutlierCount);
        Assert.Equal(4, data.MarketBenchmark.CompetitorCount);
        Assert.Equal(36m, data.MarketBenchmark.MinPriceUsd);
        Assert.Equal(49m, data.MarketBenchmark.MedianPriceUsd);
        Assert.Equal(59m, data.MarketBenchmark.FreeShippingMedianUsd);
        Assert.Equal(40m, data.MarketBenchmark.PaidShippingMedianUsd);
    }

    [Fact]
    public async Task Handle_NeverRecommendsBelowBreakEven_EvenWhenLlmSuggestsLower()
    {
        var marketService = new StubMarketResearchService(_ => new MarketSearchResponse
        {
            Listings =
            [
                new MarketListingItemDto { Id = 1, Title = "Viking Bust A", PriceUsd = 40m },
                new MarketListingItemDto { Id = 2, Title = "Viking Bust B", PriceUsd = 50m },
                new MarketListingItemDto { Id = 3, Title = "Viking Bust C", PriceUsd = 60m }
            ]
        });

        var llmClient = new StubGeminiClient("""
            {
              "strategy_summary": "Agresif giriş fiyatı.",
              "variations": [
                { "key": "Small (15cm)", "price_usd": 5.00, "tier_label": "Giriş", "reasoning": "Ucuz." }
              ]
            }
            """);

        var handler = new RunVariationPricingAgentHandler(
            marketService,
            new StubShipmentOrchestratorService(),
            llmClient);

        var result = await handler.Handle(new RunVariationPricingAgentCommand(new VariationPricingAgentRequestDto(
            ShopId: "53236321",
            Title: "Viking Jarl Bust Figurine",
            VariationType1: "Size",
            VariationValues1: "Small (15cm), Large (25cm)",
            BaseWeightGrams: 300,
            ProductionCostUsd: 8m,
            TargetNetMarginPercent: 30m,
            Provider: "Gemini",
            ApiKey: "test-key"
        )), CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<VariationPricingAgentResultDto>(result.Data);
        Assert.True(data.UsedAi);
        Assert.All(data.PricedVariations, v => Assert.True(v.RecommendedPriceUsd >= v.BreakEvenPriceUsd));
    }

    private sealed class StubMarketResearchService(Func<MarketSearchQuery, MarketSearchResponse> responder) : IMarketResearchService
    {
        public Task<MarketSearchResponse> SearchMarketAsync(MarketSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(responder(query));

        public Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(MarketAiReportCommand command, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<CompetitorShopAnalysisDto> AnalyzeCompetitorShopAsync(string shopNameOrUrl, int limit = 50, string authShopId = "53236321", CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IReadOnlyList<SeasonalTrendItem> GetSeasonalTrends(DateTime? referenceDate = null) => [];
    }

    private sealed class StubShipmentOrchestratorService : IShipmentOrchestratorService
    {
        public Task<CreateCarrierShipmentResult> CreateShipmentAsync(CreateCarrierShipmentCommand command, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ConnectCarrierResult> ConnectCarrierAsync(ConnectCarrierCommand command, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<CarrierSessionStatusDto>> GetCarrierSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CarrierSessionStatusDto>>([]);

        public Task<CarrierQuotesResponseDto> GetCarrierQuotesAsync(CarrierQuotesQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new CarrierQuotesResponseDto
            {
                CalculatedDesi = 1.0,
                BillableWeightKg = query.WeightKg,
                Quotes =
                [
                    new UnifiedCarrierQuoteDto
                    {
                        CarrierKey = "shiptomore",
                        CarrierName = "Shiptomore",
                        ServiceType = "Standard",
                        GrandTotalUsd = 14.54m,
                        GrandTotalTry = 733m,
                        EstimatedDays = "2-5 İş Günü"
                    }
                ]
            });

        public IReadOnlyList<HsCodeSearchItemDto> SearchHsCodes(string? query, int limit = 25) => [];
    }

    private sealed class StubGeminiClient(string? jsonResponse = null) : IGeminiListingApiClient
    {
        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction,
            string userPrompt,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult((jsonResponse != null, jsonResponse, (string?)null));

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
            string systemInstruction,
            string userPrompt,
            IReadOnlyList<VisionImageInput> images,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult((false, (string?)null, (string?)null));
    }
}
