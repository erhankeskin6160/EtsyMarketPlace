namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public class AiListingOptimizationUseCaseTests
{
    private sealed class FakeGeminiListingApiClient : IGeminiListingApiClient
    {
        public bool Success { get; set; } = true;
        public string? ContentToReturn { get; set; }
        public string? ErrorToReturn { get; set; }

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction,
            string userPrompt,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((Success, ContentToReturn, ErrorToReturn));
        }
    }

    private sealed class FakeShopSettingsRepository : IShopSettingsRepository
    {
        public List<SaveListingAuditRecordRequest> SavedAudits { get; } = new();

        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SavedListingAuditRecord>>(new List<SavedListingAuditRecord>());

        public Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken ct)
        {
            SavedAudits.Add(request);
            return Task.CompletedTask;
        }

        public Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken ct) => Task.FromResult<TelegramShopSettings?>(null);
        public Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken ct) => Task.FromResult<TelegramShopSettings>(null!);
        public Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken ct) => Task.FromResult<IReadOnlyList<CarrierSessionRecord>>(Array.Empty<CarrierSessionRecord>());
        public Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken ct) => Task.FromResult<CarrierSessionRecord>(null!);
        public Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken ct) => Task.FromResult<EtsyAppCredentialsRecord?>(null);
        public Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken ct) => Task.FromResult(("test", "test", "http://localhost"));
        public Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken ct) => Task.FromResult<EtsyAppCredentialsRecord>(null!);
        public Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task OptimizeListingAsync_WhenOfflineRequested_UsesRuleOptimizer()
    {
        var fakeGemini = new FakeGeminiListingApiClient();
        var fakeRepo = new FakeShopSettingsRepository();
        var ruleOptimizer = new ListingOptimizationService();
        var useCase = new AiListingOptimizationUseCase(fakeGemini, ruleOptimizer, fakeRepo);

        var cmd = new OptimizeListingCommandDto(
            ShopId: "53236321",
            Title: "Handmade Wooden Mug",
            Description: "Wooden mug for coffee",
            Tags: new[] { "mug", "coffee" },
            TargetKeyword: "wooden mug",
            DescriptionStyle: "Storytelling",
            FocusKeywords: null,
            TargetBuyerPersona: null,
            Tone: null,
            Model: null,
            ApiKey: null,
            Provider: "Offline");

        var result = await useCase.OptimizeListingAsync("123", "53236321", cmd);

        Assert.True(result.Success);
        Assert.Equal("123", result.ListingId);
        Assert.Equal("Offline (Kural Motoru)", result.Provider);
        Assert.NotEmpty(result.TitleSuggestions);
        Assert.NotEmpty(result.TagSuggestions);
        Assert.Single(fakeRepo.SavedAudits);
    }

    [Fact]
    public async Task OptimizeListingAsync_WhenGeminiConfigured_ParsesAiJsonAndSavesAudit()
    {
        var geminiJsonResponse = """
        {
            "title_suggestions": ["Handmade Wooden Coffee Mug Organic Oak Woodwork"],
            "tag_suggestions": ["wooden mug", "coffee mug", "wood cup", "oak mug", "rustic mug", "handmade mug", "wood gift", "coffee gift", "barista mug", "kitchen decor", "wood craft", "wood mug", "organic mug"],
            "material_suggestions": ["Oak Wood", "Beeswax"],
            "description_draft": "Elevate your morning routine with this handcrafted wooden mug...",
            "current_seo_score": 75,
            "optimized_seo_score": 98,
            "seo_critique": "All 13 tags filled, title optimized with primary keywords.",
            "risk_warnings": []
        }
        """;

        var fakeGemini = new FakeGeminiListingApiClient
        {
            Success = true,
            ContentToReturn = geminiJsonResponse
        };
        var fakeRepo = new FakeShopSettingsRepository();
        var ruleOptimizer = new ListingOptimizationService();
        var useCase = new AiListingOptimizationUseCase(fakeGemini, ruleOptimizer, fakeRepo);

        var cmd = new OptimizeListingCommandDto(
            ShopId: "53236321",
            Title: "Handmade Wooden Mug",
            Description: "Wooden mug for coffee",
            Tags: new[] { "mug", "coffee" },
            TargetKeyword: "wooden mug",
            DescriptionStyle: "Storytelling",
            FocusKeywords: null,
            TargetBuyerPersona: null,
            Tone: null,
            Model: "gemini-2.5-flash",
            ApiKey: "dummy-key",
            Provider: "Gemini");

        var result = await useCase.OptimizeListingAsync("123", "53236321", cmd);

        Assert.True(result.Success);
        Assert.Equal("Handmade Wooden Coffee Mug Organic Oak Woodwork", result.OptimizedTitle);
        Assert.Equal(98, result.OptimizedSeoScore);
        Assert.Equal("Gemini (Canlı API)", result.Provider);
        Assert.Single(fakeRepo.SavedAudits);
    }

    [Fact]
    public async Task OptimizeListingAsync_WhenDeepSeekConfigured_PreservesActiveProviderAndModel()
    {
        var aiJsonResponse = """
        {
            "title_suggestions": ["3D Printed Horror Mask Prop Hand-Painted Cosplay"],
            "tag_suggestions": ["horror mask", "cosplay prop", "3d printed mask", "movie prop", "halloween mask", "collector mask", "gamer gift", "wall art mask", "resin prop", "custom mask", "costume mask", "scary mask", "gift for him"],
            "material_suggestions": ["PLA Filament", "Acrylic Paint"],
            "description_draft": "Hand-painted 3D printed cosplay mask...",
            "current_seo_score": 84,
            "optimized_seo_score": 97,
            "seo_critique": "Optimized with DeepSeek-V4-Flash.",
            "risk_warnings": []
        }
        """;

        var fakeAi = new FakeGeminiListingApiClient
        {
            Success = true,
            ContentToReturn = aiJsonResponse
        };
        var fakeRepo = new FakeShopSettingsRepository();
        var ruleOptimizer = new ListingOptimizationService();
        var useCase = new AiListingOptimizationUseCase(fakeAi, ruleOptimizer, fakeRepo);

        var cmd = new OptimizeListingCommandDto(
            ShopId: "53236321",
            Title: "Horror Mask",
            Description: "3D printed mask",
            Tags: new[] { "mask" },
            TargetKeyword: "horror mask",
            DescriptionStyle: "Storytelling",
            FocusKeywords: null,
            TargetBuyerPersona: null,
            Tone: null,
            Model: "DeepSeek-V4-Flash",
            ApiKey: "sk-deepseek-test",
            Provider: "DeepSeek");

        var result = await useCase.OptimizeListingAsync("4390353802", "53236321", cmd);

        Assert.True(result.Success);
        Assert.Equal("DeepSeek (Canlı API)", result.Provider);
        Assert.Equal("DeepSeek-V4-Flash", result.AiModel);
        Assert.Single(fakeRepo.SavedAudits);
    }
}
