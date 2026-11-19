using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Features.Listings.Commands.SuggestListingVariations;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.ListingOptimization;

public sealed class SuggestListingVariationsHandlerTests
{
    [Fact]
    public async Task Handle_WithLiveLlm_ForwardsFullProductContextAndParsesProductSpecificSuggestions()
    {
        string? capturedSystemPrompt = null;
        string? capturedUserPrompt = null;
        string? capturedModel = null;
        string? capturedApiKey = null;

        var stubClient = new CapturingAiClient(
            (sys, usr, model, key) =>
            {
                capturedSystemPrompt = sys;
                capturedUserPrompt = usr;
                capturedModel = model;
                capturedApiKey = key;
                return (true, """
                    {
                      "analysis_summary": "Başlık ve açıklamadaki seramik kupa ile 11oz/15oz kapasite ve sır renkleri analiz edildi.",
                      "variations": [
                        {
                          "name": "Capacity",
                          "reason": "Kupa ilanlarında hacim seçimi dönüşümü artırır.",
                          "values": ["11 oz", "15 oz"],
                          "price_multipliers": [1.0, 1.25]
                        },
                        {
                          "name": "Glaze Color",
                          "reason": "Açıklamada geçen seramik sır renkleri korundu.",
                          "values": ["Sage Green", "Matte Cream"],
                          "price_multipliers": [1.0, 1.0]
                        }
                      ]
                    }
                    """, null);
            });

        var handler = new SuggestListingVariationsHandler(stubClient);
        var req = new SuggestListingVariationsRequestDto(
            Title: "Handmade Ceramic Coffee Mug | Botanical Speckled Tea Cup",
            Description: "Wheel-thrown stoneware mug available in 11oz or 15oz with Sage Green or Matte Cream glaze.",
            Category: "Home & Living > Kitchen & Dining > Drink & Barware > Drinkware > Mugs",
            Tags: ["ceramic mug", "coffee gift"],
            Materials: ["Stoneware Clay", "Lead Free Glaze"],
            ListingType: "physical",
            PriceUsd: 24.00m,
            CurrentVarType1: "Size",
            CurrentVarValues1: "Standard",
            Provider: "DeepSeek",
            Model: "DeepSeek-V4-Flash",
            ApiKey: "sk-deepseek-test",
            StrictNeverOffline: true);

        var resp = await handler.Handle(new SuggestListingVariationsCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var data = Assert.IsType<SuggestListingVariationsResultDto>(resp.Data);
        Assert.True(data.UsedAi);
        Assert.Equal("DeepSeek", data.Provider);
        Assert.Equal("DeepSeek-V4-Flash", capturedModel);
        Assert.Equal("sk-deepseek-test", capturedApiKey);
        Assert.Contains("seramik kupa", data.AnalysisSummary, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(capturedSystemPrompt);
        Assert.DoesNotContain("Rainbow Silk", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Small (15cm)", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(capturedUserPrompt);
        Assert.Contains("Handmade Ceramic Coffee Mug", capturedUserPrompt);
        Assert.Contains("Wheel-thrown stoneware mug", capturedUserPrompt);
        Assert.Contains("ceramic mug", capturedUserPrompt);
        Assert.Contains("Stoneware Clay", capturedUserPrompt);
        Assert.Contains("Drinkware > Mugs", capturedUserPrompt);
        Assert.Contains("current_variations", capturedUserPrompt);

        Assert.Equal(2, data.SuggestedVariations.Count);
        Assert.Equal("Capacity", data.SuggestedVariations[0].Name);
        Assert.Equal(["11 oz", "15 oz"], data.SuggestedVariations[0].Values);
        Assert.Equal(4, data.SuggestedOfferings.Count);

        var smallMug = data.SuggestedOfferings.First(o => o.Key.StartsWith("11 oz"));
        var largeMug = data.SuggestedOfferings.First(o => o.Key.StartsWith("15 oz"));
        Assert.True(largeMug.Price > smallMug.Price);
    }

    [Fact]
    public async Task Handle_StrictNeverOffline_WithoutApiKey_ReturnsErrorInsteadOfSilentFallback()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (false, null, "should not be called"));
        var handler = new SuggestListingVariationsHandler(stubClient);

        var req = new SuggestListingVariationsRequestDto(
            Title: "Thinking Alien Desk Figurine",
            Description: "Minimalist 12cm matte black resin alien desk decor.",
            Provider: "DeepSeek",
            Model: "DeepSeek-V4-Flash",
            ApiKey: "",
            StrictNeverOffline: true);

        var resp = await handler.Handle(new SuggestListingVariationsCommand(req), CancellationToken.None);

        Assert.False(resp.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("DeepSeek", resp.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_StrictNeverOffline_WhenLlmFails_ReturnsBadGatewayError()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (false, null, "401 Unauthorized"));
        var handler = new SuggestListingVariationsHandler(stubClient);

        var req = new SuggestListingVariationsRequestDto(
            Title: "Thinking Alien Desk Figurine",
            Description: "Minimalist 12cm matte black alien sculpture.",
            Provider: "DeepSeek",
            Model: "DeepSeek-V4-Flash",
            ApiKey: "sk-bad",
            StrictNeverOffline: true);

        var resp = await handler.Handle(new SuggestListingVariationsCommand(req), CancellationToken.None);

        Assert.False(resp.Success);
        Assert.Equal((int)HttpStatusCode.BadGateway, resp.StatusCode);
        Assert.Contains("401 Unauthorized", resp.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_OfflineMode_GeneratesDistinctProductSpecificVariationsForDifferentProducts()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (false, null, null));
        var handler = new SuggestListingVariationsHandler(stubClient);

        var mugResp = await handler.Handle(new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
            Title: "Handmade Ceramic Coffee Mug",
            Description: "Stoneware cup 11 oz and 15 oz in Sage Green and Matte White.",
            Category: "Home & Living > Drinkware > Mugs",
            Provider: "Offline",
            StrictNeverOffline: false)), CancellationToken.None);

        var digitalResp = await handler.Handle(new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
            Title: "Botanical Line Art Printable Poster Set",
            Description: "Instant digital download wall art PDF 8x10 in and 12x16 in.",
            ListingType: "digital",
            Provider: "Offline",
            StrictNeverOffline: false)), CancellationToken.None);

        var alienResp = await handler.Handle(new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
            Title: "Thinking Alien Desk Figurine",
            Description: "Minimalist modern alien sculpture 12 cm height, matte black finish.",
            Materials: ["PLA"],
            Provider: "Offline",
            StrictNeverOffline: false)), CancellationToken.None);

        Assert.True(mugResp.Success);
        Assert.True(digitalResp.Success);
        Assert.True(alienResp.Success);

        var mug = Assert.IsType<SuggestListingVariationsResultDto>(mugResp.Data);
        var digital = Assert.IsType<SuggestListingVariationsResultDto>(digitalResp.Data);
        var alien = Assert.IsType<SuggestListingVariationsResultDto>(alienResp.Data);

        Assert.Equal("Capacity", mug.SuggestedVariations[0].Name);
        Assert.Contains("11 oz", mug.SuggestedVariations[0].Values);

        Assert.Equal("Print Size / Format", digital.SuggestedVariations[0].Name);
        Assert.Single(digital.SuggestedVariations);

        Assert.Equal("Size", alien.SuggestedVariations[0].Name);
        Assert.Contains(alien.SuggestedVariations[0].Values, v => v.Contains("12 cm", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Matte Black", alien.SuggestedVariations[1].Values);
        Assert.DoesNotContain("Rainbow Silk", alien.SuggestedVariations[1].Values);
        Assert.DoesNotContain("Gökkuşağı / Rainbow", alien.SuggestedVariations[1].Values);
    }

    private sealed class CapturingAiClient(
        Func<string, string, string, string, (bool Success, string? Content, string? ErrorMessage)> responder)
        : IGeminiListingApiClient
    {
        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction,
            string userPrompt,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(responder(systemInstruction, userPrompt, model, apiKey));

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
            string systemInstruction,
            string userPrompt,
            IReadOnlyList<VisionImageInput> images,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
