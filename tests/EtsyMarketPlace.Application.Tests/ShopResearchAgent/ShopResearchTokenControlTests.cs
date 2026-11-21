namespace EtsyMarketPlace.Application.Tests.ShopResearchAgent;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.ProductOpportunity;
using EtsyMarketPlace.Application.SeasonalTrends;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Application.ShopResearchAgent.Services;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using Xunit;

public sealed class ShopResearchTokenControlTests
{
    [Fact]
    public void ShopDnaBuilder_Defaults_ScheduleCadence_To_Off_ManualOnly()
    {
        var builder = new ShopDnaBuilderService();
        var profile = builder.BuildFromListings("user-1", "53236321",
        [
            MakeListing("1001", "Minimalist 3D Printed Desk Organizer", 34.90m, ["desk organizer", "3d printed gift"])
        ]);

        Assert.Equal(ResearchScheduleCadence.Off, profile.ScheduleCadence);
        Assert.False(profile.IsScheduleDue(DateTime.UtcNow));
    }

    [Fact]
    public async Task Orchestrator_EconomicMode_MakesZeroLlmCalls_EvenWhenApiKeyProvided()
    {
        var fakeLlm = new FakeUsageLlmClient();
        var fakeRecorder = new FakeAiUsageRecorder();
        var orchestrator = CreateOrchestrator(fakeLlm, fakeRecorder);

        var run = await orchestrator.RunResearchAsync(
            "user-1",
            "53236321",
            ResearchTriggerType.Manual,
            new RunShopResearchRequest(
                FocusHint: null,
                MaxCandidates: 5,
                Model: "deepseek-chat",
                ApiKey: "sk-test-key",
                AiMode: ResearchAiMode.Economic));

        Assert.Equal(ResearchRunStatus.Completed, run.Status);
        Assert.Equal(0, fakeLlm.CallCount);
        Assert.Empty(fakeRecorder.RecordedCalls);
        Assert.Contains("0 token (Ekonomik Mod)", run.Summary);
    }

    [Fact]
    public async Task Orchestrator_BalancedMode_SkipsQueryPlanningLlm_AndRecordsBlueprintUsage()
    {
        var fakeLlm = new FakeUsageLlmClient();
        var fakeRecorder = new FakeAiUsageRecorder();
        var orchestrator = CreateOrchestrator(fakeLlm, fakeRecorder);

        var run = await orchestrator.RunResearchAsync(
            "user-1",
            "53236321",
            ResearchTriggerType.Manual,
            new RunShopResearchRequest(
                FocusHint: null,
                MaxCandidates: 5,
                Model: "deepseek-chat",
                ApiKey: "sk-test-key",
                AiMode: ResearchAiMode.Balanced));

        Assert.Equal(ResearchRunStatus.Completed, run.Status);
        Assert.Equal(1, fakeLlm.CallCount);
        Assert.Equal(1200, fakeLlm.CapturedMaxOutputTokens.Single());
        Assert.Single(fakeRecorder.RecordedCalls);
        Assert.Equal("Mağaza Araştırma Ajanı", fakeRecorder.RecordedCalls[0].ModuleName);
        Assert.Contains("Ürün Blueprint", fakeRecorder.RecordedCalls[0].Note);
        Assert.Equal(450, fakeRecorder.RecordedCalls[0].Result.TotalTokens);
    }

    [Fact]
    public async Task Orchestrator_FullAiMode_CapsMaxTokens_AndRecordsBothCallsUnderResearchAgentModule()
    {
        var fakeLlm = new FakeUsageLlmClient();
        var fakeRecorder = new FakeAiUsageRecorder();
        var orchestrator = CreateOrchestrator(fakeLlm, fakeRecorder);

        var run = await orchestrator.RunResearchAsync(
            "user-1",
            "53236321",
            ResearchTriggerType.Manual,
            new RunShopResearchRequest(
                FocusHint: null,
                MaxCandidates: 5,
                Model: "deepseek-chat",
                ApiKey: "sk-test-key",
                AiMode: ResearchAiMode.FullAi));

        Assert.Equal(ResearchRunStatus.Completed, run.Status);
        Assert.Equal(2, fakeLlm.CallCount);
        Assert.Equal(new int?[] { 500, 1200 }, fakeLlm.CapturedMaxOutputTokens);
        Assert.Equal(2, fakeRecorder.RecordedCalls.Count);
        Assert.All(fakeRecorder.RecordedCalls, c => Assert.Equal("Mağaza Araştırma Ajanı", c.ModuleName));
        Assert.Contains(fakeRecorder.RecordedCalls, c => c.Note == "Sorgu Planlama");
        Assert.Contains(fakeRecorder.RecordedCalls, c => c.Note != null && c.Note.StartsWith("Ürün Blueprint"));
        Assert.Contains("900 token", run.Summary);
    }

    private static ShopResearchAgentOrchestrator CreateOrchestrator(
        IGeminiListingApiClient llmClient,
        IAiUsageRecorder usageRecorder)
    {
        var repo = new InMemoryRepo();
        var listingService = new StubListingService();
        var marketService = new StubMarketService();
        return new ShopResearchAgentOrchestrator(
            repo,
            listingService,
            marketService,
            new ShopDnaBuilderService(),
            new CannibalizationGuardService(),
            new ProductOpportunityScorer(),
            llmClient: llmClient,
            usageRecorder: usageRecorder);
    }

    private sealed class FakeUsageLlmClient : IGeminiListingApiClient
    {
        public int CallCount { get; private set; }
        public List<int?> CapturedMaxOutputTokens { get; } = [];

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction, string userPrompt, string model, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult<(bool, string?, string?)>((true, "{}", null));

        public Task<LlmJsonResult> GenerateOptimizationJsonWithUsageAsync(
            string systemInstruction,
            string userPrompt,
            string model,
            string apiKey,
            int? maxOutputTokens = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedMaxOutputTokens.Add(maxOutputTokens);

            var content = maxOutputTokens == 500
                ? "{\"queries\":[{\"query\":\"minimalist desk organizer\",\"mode\":\"exploit\",\"reason\":\"dna\"},{\"query\":\"wooden pen holder\",\"mode\":\"explore\",\"reason\":\"explore\"}]}"
                : "{\"blueprints\":[]}";

            return Task.FromResult(new LlmJsonResult(
                Success: true,
                Content: content,
                ErrorMessage: null,
                PromptTokens: 300,
                CompletionTokens: 150,
                TotalTokens: 450,
                Provider: "DeepSeek",
                ResolvedModel: "deepseek-chat"));
        }

        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
            string systemInstruction, string userPrompt, IReadOnlyList<VisionImageInput> images, string model, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult<(bool, string?, string?)>((true, "{}", null));
    }

    private sealed class FakeAiUsageRecorder : IAiUsageRecorder
    {
        public List<(string ModuleName, LlmJsonResult Result, string? Note)> RecordedCalls { get; } = [];

        public Task RecordAsync(string moduleName, LlmJsonResult result, string? note = null, CancellationToken cancellationToken = default)
        {
            RecordedCalls.Add((moduleName, result, note));
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRepo : IShopResearchRepository
    {
        private ShopDnaProfile? _dna;
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ShopDnaProfile?> GetDnaProfileAsync(string ownerUserId, string shopId, CancellationToken ct = default) => Task.FromResult(_dna);
        public Task UpsertDnaProfileAsync(ShopDnaProfile profile, CancellationToken ct = default) { _dna = profile; return Task.CompletedTask; }
        public Task<IReadOnlyList<ShopDnaProfile>> GetScheduledProfilesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ShopDnaProfile>>([]);
        public Task UpdateLastScheduledRunAsync(string ownerUserId, string shopId, DateTime utcNow, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveRunAsync(ResearchRun run, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ResearchRun>> GetRunsAsync(string ownerUserId, string shopId, int limit = 20, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ResearchRun>>([]);
        public Task<ResearchRun?> GetRunByIdAsync(string ownerUserId, string shopId, Guid runId, CancellationToken ct = default) => Task.FromResult<ResearchRun?>(null);
        public Task<ResearchCandidate?> GetCandidateByIdAsync(string ownerUserId, string shopId, Guid candidateId, CancellationToken ct = default) => Task.FromResult<ResearchCandidate?>(null);
        public Task UpdateCandidateDecisionAsync(string ownerUserId, string shopId, Guid candidateId, CandidateDecisionStatus decision, CandidateRejectionReason reason, CancellationToken ct = default) => Task.CompletedTask;
        public Task AppendDecisionEventAsync(CandidateDecisionEvent decisionEvent, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CandidateDecisionEvent>> GetRecentDecisionEventsAsync(string ownerUserId, string shopId, int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CandidateDecisionEvent>>([]);
        public Task<IReadOnlyList<BanditArmState>> GetBanditArmsAsync(string ownerUserId, string shopId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BanditArmState>>([]);
        public Task UpsertBanditArmAsync(BanditArmState arm, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ShopListingItemDto MakeListing(string id, string title, decimal price, string[] tags)
        => new(
            Rank: 1,
            ListingId: id,
            Title: title,
            Description: string.Empty,
            Tags: tags,
            Materials: ["PLA"],
            Price: price,
            PriceAmount: price,
            Currency: "USD",
            CurrencyCode: "USD",
            Quantity: 10,
            Views: 100,
            Favorites: 15,
            NumFavorers: 15,
            Images: [],
            Thumbnail: string.Empty,
            PrimaryImageUrl: string.Empty,
            SeoScore: 80,
            AiScore: null,
            Status: "active",
            IsAiAudited: false,
            ResultJson: null,
            SeoNeeds: string.Empty,
            SeoStrengths: string.Empty,
            StructuralNeeds: [],
            RiskWarnings: [],
            HasSavedAudit: false,
            SavedAudit: null);

    private sealed class StubListingService : IEtsyListingService
    {
        public Task<IReadOnlyList<ShopListingItemDto>> GetShopListingsAsync(string shopId, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ShopListingItemDto>>(
            [
                MakeListing("1", "3D Printed Matte Cable Clip", 32m, ["desk organizer", "cable clip", "office gift"])
            ]);

        public Task<ListingUpdateResultDto> UpdateListingAsync(string shopId, string listingId, UpdateListingRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ListingUpdateResultDto> UpdateListingStateAsync(string shopId, long listingId, string state, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubMarketService : IMarketResearchService
    {
        public Task<MarketSearchResponse> SearchMarketAsync(MarketSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new MarketSearchResponse
            {
                Keyword = query.Keyword,
                Total = 180,
                Kpis = new MarketSummaryKpisDto
                {
                    TotalListings = 180,
                    MedianPrice = 42m,
                    AveragePrice = 44m,
                    TopShopConcentrationPct = 15.0,
                    FreeShippingRatioPct = 40.0,
                    AverageEstimatedMonthlySales = 9.0,
                    BestsellerCount = 10
                },
                Listings =
                [
                    new MarketListingItemDto
                    {
                        Id = 9001,
                        Title = "Sculptural Ribbed Desk Lamp Matte Finish",
                        PriceUsd = 46m,
                        Favorites = 340,
                        Views = 1800,
                        ReviewCount = 42,
                        EstimatedMonthlySales = 16,
                        MarketScore = 88,
                        ImageUrl = "https://i.etsystatic.com/sample-lamp.jpg",
                        ListingUrl = "https://www.etsy.com/listing/9001",
                        Tags = ["desk lamp", "nordic decor", "gift idea"]
                    }
                ]
            });

        public Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(MarketAiReportCommand command, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<CompetitorShopAnalysisDto> AnalyzeCompetitorShopAsync(string shopNameOrUrl, int limit = 50, string authShopId = "53236321", CancellationToken cancellationToken = default)
            => Task.FromResult(new CompetitorShopAnalysisDto());

        public IReadOnlyList<SeasonalTrendItem> GetSeasonalTrends(DateTime? referenceDate = null) => [];
    }
}
