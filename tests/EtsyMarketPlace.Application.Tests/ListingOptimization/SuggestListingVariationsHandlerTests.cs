using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Features.Listings.Commands.SuggestListingVariations;
using EtsyMarketPlace.Application.Features.Listings.Validation;
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

    [Fact]
    public async Task Handle_WithLiveLlmReturningSlashOrCommaValues_SanitizesOptionsAndMaintainsFullOfferingsParity()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (true, """
            {
              "analysis_summary": "Ölçü ve kaplama seçenekleri analiz edildi.",
              "variations": [
                {
                  "name": "Print Size",
                  "reason": "Çerçeve boyutları ayrıştırıldı.",
                  "values": ["A4 / Letter", "11x14 in", "12x16 in", "16x20 in", "18x24 in"],
                  "price_multipliers": [1.0, 1.1, 1.2, 1.35, 1.5]
                },
                {
                  "name": "Frame Finish",
                  "reason": "Çerçeve bitişleri belirlendi.",
                  "values": ["Black, Gold", "Natural Oak", "Walnut", "Matte White", "Brass / Bronze"],
                  "price_multipliers": [1.0, 1.0, 1.05, 1.0, 1.15]
                }
              ]
            }
            """, null));

        var handler = new SuggestListingVariationsHandler(stubClient);
        var req = new SuggestListingVariationsRequestDto(
            Title: "Botanical Wall Art Print",
            Description: "Museum quality matte art print in multiple frame finishes.",
            PriceUsd: 25.00m,
            Provider: "OpenAI",
            Model: "gpt-4o-mini",
            ApiKey: "sk-openai-test",
            StrictNeverOffline: true);

        var resp = await handler.Handle(new SuggestListingVariationsCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var data = Assert.IsType<SuggestListingVariationsResultDto>(resp.Data);
        Assert.Equal(2, data.SuggestedVariations.Count);
        Assert.Equal(5, data.SuggestedVariations[0].Values.Count);
        Assert.Equal(5, data.SuggestedVariations[1].Values.Count);

        foreach (var group in data.SuggestedVariations)
        {
            foreach (var val in group.Values)
            {
                Assert.DoesNotContain("/", val);
                Assert.DoesNotContain(",", val);
            }
        }

        Assert.Contains("A4 - Letter", data.SuggestedVariations[0].Values);
        Assert.Contains("Black & Gold", data.SuggestedVariations[1].Values);
        Assert.Contains("Brass - Bronze", data.SuggestedVariations[1].Values);

        // Full 5 x 5 = 25 combinations without premature 16-row truncation
        Assert.Equal(25, data.SuggestedOfferings.Count);
        var expectedKeys = data.SuggestedVariations[0].Values
            .SelectMany(g1 => data.SuggestedVariations[1].Values.Select(g2 => $"{g1} / {g2}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expectedKeys.Count, data.SuggestedOfferings.Select(o => o.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var o in data.SuggestedOfferings)
        {
            Assert.Contains(o.Key, expectedKeys);
        }
    }

    [Fact]
    public async Task Handle_WithExtraGroupsAndDuplicateValues_DeduplicatesAndCapsToTwoGroupsAndFiveValues()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (true, """
            {
              "analysis_summary": "Ürüne özel gruplar oluşturuldu.",
              "variations": [
                {
                  "name": "Size",
                  "reason": "Ölçü seçenekleri",
                  "values": ["Small", "small", "Medium", "Large", "XL", "XXL", "Giant"],
                  "price_multipliers": [1.0, 1.0, 1.15, 1.3, 1.45, 1.6, 1.8]
                },
                {
                  "name": "size",
                  "reason": "Yinelenen grup atlanmalı",
                  "values": ["Alpha", "Beta"]
                },
                {
                  "name": "Color",
                  "reason": "Renk seçenekleri",
                  "values": ["Black", "White", "Walnut"],
                  "price_multipliers": [1.0, 1.0, 1.1]
                },
                {
                  "name": "Packaging",
                  "reason": "Üçüncü grup atlanmalı",
                  "values": ["Standard", "Gift Box"]
                }
              ]
            }
            """, null));

        var handler = new SuggestListingVariationsHandler(stubClient);
        var req = new SuggestListingVariationsRequestDto(
            Title: "Minimalist Walnut Wood Desk Organizer",
            Provider: "Gemini",
            ApiKey: "gm-test-key",
            PriceUsd: 30.00m);

        var resp = await handler.Handle(new SuggestListingVariationsCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var data = Assert.IsType<SuggestListingVariationsResultDto>(resp.Data);
        Assert.Equal(2, data.SuggestedVariations.Count);
        Assert.Equal("Size", data.SuggestedVariations[0].Name);
        Assert.Equal("Color", data.SuggestedVariations[1].Name);
        Assert.Equal(["Small", "Medium", "Large", "XL", "XXL"], data.SuggestedVariations[0].Values);
    }

    [Fact]
    public async Task Handle_NonStrictOfflineFallback_WhenLlmFails_ReturnsHeuristicWithWarningMessage()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (false, null, "429 quota exceeded"));
        var handler = new SuggestListingVariationsHandler(stubClient);

        var req = new SuggestListingVariationsRequestDto(
            Title: "Handmade Soy Scented Candle in Amber Jar",
            Description: "Hand-poured 8 oz natural soy wax candle.",
            Provider: "Gemini",
            ApiKey: "gm-test-key",
            StrictNeverOffline: false);

        var resp = await handler.Handle(new SuggestListingVariationsCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var data = Assert.IsType<SuggestListingVariationsResultDto>(resp.Data);
        Assert.False(data.UsedAi);
        Assert.False(string.IsNullOrWhiteSpace(data.WarningMessage));
        Assert.Contains("429 quota exceeded", data.WarningMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(data.SuggestedVariations, g => g.Name.Contains("Jar Size", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("DeepSeek", "deepseek-chat")]
    [InlineData("OpenAI", "gpt-4o-mini")]
    [InlineData("Claude", "claude-3-7-sonnet-20250219")]
    [InlineData("Grok", "grok-3")]
    [InlineData("Gemini", "gemini-2.5-flash")]
    public async Task Handle_WhenModelIsEmpty_ResolvesDefaultModelPerProvider(string provider, string expectedModel)
    {
        string? capturedModel = null;
        var stubClient = new CapturingAiClient((_, _, model, _) =>
        {
            capturedModel = model;
            return (true, """
                {
                  "analysis_summary": "Önerildi.",
                  "variations": [
                    { "name": "Finish", "reason": "Test", "values": ["Matte", "Gloss"], "price_multipliers": [1.0, 1.1] }
                  ]
                }
                """, null);
        });

        var handler = new SuggestListingVariationsHandler(stubClient);
        var req = new SuggestListingVariationsRequestDto(
            Title: "Handmade Ceramic Planter",
            Provider: provider,
            Model: "",
            ApiKey: "sk-valid-key");

        var resp = await handler.Handle(new SuggestListingVariationsCommand(req), CancellationToken.None);

        Assert.True(resp.Success);
        var data = Assert.IsType<SuggestListingVariationsResultDto>(resp.Data);
        Assert.Equal(expectedModel, capturedModel);
        Assert.Equal(expectedModel, data.Model);
    }

    [Fact]
    public void Validator_ValidatesTitleDescriptionAndPriceUsd()
    {
        var validator = new SuggestListingVariationsCommandValidator();

        var nullReqResult = validator.Validate(new SuggestListingVariationsCommand(null!));
        Assert.False(nullReqResult.IsValid);

        var emptyResult = validator.Validate(new SuggestListingVariationsCommand(
            new SuggestListingVariationsRequestDto(Title: " ", Description: "")));
        Assert.False(emptyResult.IsValid);

        var negativePriceResult = validator.Validate(new SuggestListingVariationsCommand(
            new SuggestListingVariationsRequestDto(Title: "Wooden Vase", PriceUsd: -1m)));
        Assert.False(negativePriceResult.IsValid);

        var descOnlyValid = validator.Validate(new SuggestListingVariationsCommand(
            new SuggestListingVariationsRequestDto(Title: "", Description: "15cm hand-carved olive wood bowl", PriceUsd: 0m)));
        Assert.True(descOnlyValid.IsValid);
    }

    [Fact]
    public async Task Handle_OfflineHeuristic_PreventsSubstringFalsePositivesAcrossProductTypes()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (false, null, null));
        var handler = new SuggestListingVariationsHandler(stubClient);

        // "3D Printed Thinking Alien Figurine" contains "printed" — must stay Figurine, NOT Wall Art
        var printedFigurine = Assert.IsType<SuggestListingVariationsResultDto>((await handler.Handle(
            new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
                Title: "3D Printed Thinking Alien Figurine",
                Description: "Modern desk sculpture",
                Provider: "Offline")), CancellationToken.None)).Data);
        Assert.Equal("Size", printedFigurine.SuggestedVariations[0].Name);
        Assert.Equal("Finish", printedFigurine.SuggestedVariations[1].Name);

        // "Gold Drop Earrings" contains "ring" — must stay Earrings, NOT Ring Size
        var earrings = Assert.IsType<SuggestListingVariationsResultDto>((await handler.Handle(
            new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
                Title: "Handmade Gold Drop Earrings",
                Description: "Lightweight hypoallergenic jewelry",
                Provider: "Offline")), CancellationToken.None)).Data);
        Assert.Contains(earrings.SuggestedVariations, g => g.Name == "Backing Style");
        Assert.DoesNotContain(earrings.SuggestedVariations, g => g.Name == "Ring Size");

        // "Stainless Steel Pendant Necklace" contains "tee" — must stay Necklace, NOT T-shirt S/M/L/XL
        var steelNecklace = Assert.IsType<SuggestListingVariationsResultDto>((await handler.Handle(
            new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
                Title: "Stainless Steel Minimalist Pendant Necklace",
                Description: "Hypoallergenic waterproof chain",
                Provider: "Offline")), CancellationToken.None)).Data);
        Assert.Contains(steelNecklace.SuggestedVariations, g => g.Name == "Chain Length");

        // "Detailed Speckled Ceramic Vase" contains "led" twice — must stay Vase, NOT LED Lamp
        var vase = Assert.IsType<SuggestListingVariationsResultDto>((await handler.Handle(
            new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
                Title: "Detailed Speckled Ceramic Vase",
                Description: "Wheel thrown stoneware flower vessel",
                Provider: "Offline")), CancellationToken.None)).Data);
        Assert.Contains(vase.SuggestedVariations, g => g.Name == "Pot Size");
        Assert.DoesNotContain(vase.SuggestedVariations, g => g.Name == "Light Color");

        // "Museum Quality Canvas Wall Art" contains "mum" — must stay Wall Art, NOT Scented Candle
        var wallArt = Assert.IsType<SuggestListingVariationsResultDto>((await handler.Handle(
            new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
                Title: "Museum Quality Canvas Wall Art",
                Description: "Archival fine art print",
                Provider: "Offline")), CancellationToken.None)).Data);
        Assert.Contains(wallArt.SuggestedVariations, g => g.Name == "Print Size");
        Assert.DoesNotContain(wallArt.SuggestedVariations, g => g.Name == "Scent");

        // "Minimalist Wooden Candle Holder" must NOT be classified as a scented candle
        var candleHolder = Assert.IsType<SuggestListingVariationsResultDto>((await handler.Handle(
            new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
                Title: "Minimalist Walnut Candle Holder",
                Description: "Hand-turned wooden tea light holder",
                Provider: "Offline")), CancellationToken.None)).Data);
        Assert.DoesNotContain(candleHolder.SuggestedVariations, g => g.Name == "Scent");
        Assert.DoesNotContain(candleHolder.SuggestedVariations, g => g.Name == "Jar Size");
    }

    [Fact]
    public async Task Handle_OfflineHeuristic_DoesNotDoubleExtractCompoundColorNames()
    {
        var stubClient = new CapturingAiClient((_, _, _, _) => (false, null, null));
        var handler = new SuggestListingVariationsHandler(stubClient);

        var mugResult = Assert.IsType<SuggestListingVariationsResultDto>((await handler.Handle(
            new SuggestListingVariationsCommand(new SuggestListingVariationsRequestDto(
                Title: "Ceramic Coffee Mug",
                Description: "Available in Sage Green, Rose Gold, Ivory White and Navy Blue glazes.",
                Provider: "Offline")), CancellationToken.None)).Data);

        var colorGroup = mugResult.SuggestedVariations.First(g => g.Name == "Accent Color");
        Assert.Contains("Sage Green", colorGroup.Values);
        Assert.Contains("Rose Gold", colorGroup.Values);
        Assert.Contains("Ivory White", colorGroup.Values);
        Assert.Contains("Navy Blue", colorGroup.Values);
        Assert.DoesNotContain("Forest Green", colorGroup.Values);
        Assert.DoesNotContain("Champagne Gold", colorGroup.Values);
        Assert.DoesNotContain("Warm White", colorGroup.Values);
        Assert.DoesNotContain("Cobalt Blue", colorGroup.Values);
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
