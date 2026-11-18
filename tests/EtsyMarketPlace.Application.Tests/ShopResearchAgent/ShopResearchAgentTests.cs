namespace EtsyMarketPlace.Application.Tests.ShopResearchAgent;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.ProductOpportunity;
using EtsyMarketPlace.Application.SeasonalTrends;
using EtsyMarketPlace.Application.ShopResearchAgent.Services;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using EtsyMarketPlace.Infrastructure.ShopResearchAgent;
using Xunit;

public sealed class ShopResearchAgentTests
{
    [Fact]
    public void ShopDnaBuilder_Detects3DPrintSignals_AndExtractsPriceBandAndTags()
    {
        var builder = new ShopDnaBuilderService();
        var listings = new List<ShopListingItemDto>
        {
            CreateListing("1", "Articulated Dragon 3D Printed Desk Toy PLA", 28m, ["3d print", "dragon", "fidget"], ["PLA"]),
            CreateListing("2", "Geometric 3D Printed Planter Pot STL", 22m, ["planter", "3d printed", "desk decor"], ["PLA"]),
            CreateListing("3", "Cute Miniature Figurine Gift", 34m, ["figurine", "gift", "3d print"], ["Resin"])
        };

        var existing = new ShopDnaProfile
        {
            OwnerUserId = "user-1",
            ShopId = "53236321",
            ForbiddenKeywords = ["disney"],
            ScheduleCadence = ResearchScheduleCadence.Weekly
        };

        var dna = builder.BuildFromListings("user-1", "53236321", listings, existing);

        Assert.True(dna.Supports3DPrint);
        Assert.Equal(2, dna.DnaVersion);
        Assert.Contains("disney", dna.ForbiddenKeywords);
        Assert.Equal(ResearchScheduleCadence.Weekly, dna.ScheduleCadence);
        Assert.True(dna.TargetPriceMinUsd >= 5m && dna.TargetPriceMaxUsd > dna.TargetPriceMinUsd);
        Assert.Contains("3d print", dna.TopPerformingTags);
    }

    [Fact]
    public void CannibalizationGuard_BlocksNearDuplicateListing_AndAllowsDistinctProduct()
    {
        var guard = new CannibalizationGuardService();
        var own = new List<ShopListingItemDto>
        {
            CreateListing("1", "Articulated Crystal Dragon Desk Fidget Toy", 30m, ["dragon", "fidget", "desk toy"], ["PLA"])
        };

        var duplicate = guard.Check("Articulated Crystal Dragon Desk Fidget", ["dragon", "fidget", "desk toy"], own);
        var distinct = guard.Check("Minimalist Walnut Cable Organizer Stand", ["cable organizer", "walnut", "desk"], own);

        Assert.True(duplicate.IsBlocked);
        Assert.True(duplicate.Score >= 70);
        Assert.False(distinct.IsBlocked);
        Assert.True(distinct.Score < 45);
    }

    [Fact]
    public void ShopDnaProfile_IsScheduleDue_RespectsDailyWeeklyMonthlyAndOff()
    {
        var now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

        var off = new ShopDnaProfile { ScheduleCadence = ResearchScheduleCadence.Off, LastScheduledRunAtUtc = null };
        var dailyDue = new ShopDnaProfile { ScheduleCadence = ResearchScheduleCadence.Daily, LastScheduledRunAtUtc = now.AddHours(-24) };
        var dailyNotDue = new ShopDnaProfile { ScheduleCadence = ResearchScheduleCadence.Daily, LastScheduledRunAtUtc = now.AddHours(-5) };
        var weeklyDue = new ShopDnaProfile { ScheduleCadence = ResearchScheduleCadence.Weekly, LastScheduledRunAtUtc = now.AddDays(-8) };
        var monthlyNotDue = new ShopDnaProfile { ScheduleCadence = ResearchScheduleCadence.Monthly, LastScheduledRunAtUtc = now.AddDays(-10) };

        Assert.False(off.IsScheduleDue(now));
        Assert.True(dailyDue.IsScheduleDue(now));
        Assert.False(dailyNotDue.IsScheduleDue(now));
        Assert.True(weeklyDue.IsScheduleDue(now));
        Assert.False(monthlyNotDue.IsScheduleDue(now));
    }

    [Fact]
    public async Task Orchestrator_FiltersRiskyAndCannibalizingCandidates_AndIsolatesPerOwnerUser()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"shop_research_test_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteShopResearchRepository(dbPath);
            await repo.InitializeAsync();

            var ownListings = new List<ShopListingItemDto>
            {
                CreateListing("101", "Geometric Desk Planter Pot", 28m, ["planter", "desk decor", "pot"], ["PLA"])
            };

            var marketItems = new List<MarketListingItemDto>
            {
                new()
                {
                    Id = 1,
                    Title = "Disney Mickey Mouse Desk Lamp",
                    PriceUsd = 35m,
                    Favorites = 500,
                    EstimatedMonthlySales = 20,
                    SeoScore = 80,
                    MarketScore = 92,
                    Tags = ["disney", "lamp"]
                },
                new()
                {
                    Id = 2,
                    Title = "Geometric Desk Planter Pot",
                    PriceUsd = 29m,
                    Favorites = 300,
                    EstimatedMonthlySales = 15,
                    SeoScore = 70,
                    MarketScore = 88,
                    Tags = ["planter", "desk decor", "pot"]
                },
                new()
                {
                    Id = 3,
                    Title = "Modular Hexagonal Wall Pen Holder Organizer",
                    PriceUsd = 32m,
                    Favorites = 420,
                    EstimatedMonthlySales = 25,
                    SeoScore = 62,
                    MarketScore = 90,
                    HasFreeShipping = true,
                    ListingUrl = "https://www.etsy.com/listing/3",
                    Tags = ["pen holder", "wall organizer", "desk decor", "office gift"]
                }
            };

            var orchestrator = new ShopResearchAgentOrchestrator(
                repo,
                new StubEtsyListingService(ownListings),
                new StubMarketResearchService(marketItems),
                new ShopDnaBuilderService(),
                new CannibalizationGuardService(),
                new ProductOpportunityScorer());

            var run = await orchestrator.RunResearchAsync("owner-A", "53236321", ResearchTriggerType.Manual);

            Assert.Equal(ResearchRunStatus.Completed, run.Status);
            Assert.Single(run.Candidates);
            Assert.Equal("Modular Hexagonal Wall Pen Holder Organizer", run.Candidates[0].Title);
            Assert.True(run.Candidates[0].EstimatedNetProfitUsd > 0m);
            Assert.True(run.Candidates[0].SuggestedTitle.Length <= 140);
            Assert.True(run.Candidates[0].SuggestedTags.Count <= 13);

            // Strict account isolation check: owner-B must NOT see owner-A's runs
            var ownerBRuns = await repo.GetRunsAsync("owner-B", "53236321");
            Assert.Empty(ownerBRuns);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }

    private static ShopListingItemDto CreateListing(
        string id,
        string title,
        decimal price,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> materials) => new(
            Rank: 1,
            ListingId: id,
            Title: title,
            Description: title,
            Tags: tags,
            Materials: materials,
            Price: price,
            PriceAmount: price,
            Currency: "USD",
            CurrencyCode: "USD",
            Quantity: 10,
            Views: 100,
            Favorites: 20,
            NumFavorers: 20,
            Images: Array.Empty<string>(),
            Thumbnail: string.Empty,
            PrimaryImageUrl: string.Empty,
            SeoScore: 80,
            AiScore: 85,
            Status: "active",
            IsAiAudited: true,
            ResultJson: null,
            SeoNeeds: string.Empty,
            SeoStrengths: string.Empty,
            StructuralNeeds: Array.Empty<string>(),
            RiskWarnings: Array.Empty<string>(),
            HasSavedAudit: false,
            SavedAudit: null);

    private sealed class StubEtsyListingService(IReadOnlyList<ShopListingItemDto> items) : IEtsyListingService
    {
        public Task<IReadOnlyList<ShopListingItemDto>> GetShopListingsAsync(string shopId, int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult(items);

        public Task<ListingUpdateResultDto> UpdateListingAsync(string shopId, string listingId, UpdateListingRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ListingUpdateResultDto> UpdateListingStateAsync(string shopId, long listingId, string state, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubMarketResearchService(List<MarketListingItemDto> listings) : IMarketResearchService
    {
        public Task<MarketSearchResponse> SearchMarketAsync(MarketSearchQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MarketSearchResponse
            {
                Keyword = query.Keyword,
                Total = listings.Count,
                Kpis = new MarketSummaryKpisDto
                {
                    TotalListings = 120,
                    MedianPrice = 30m,
                    AveragePrice = 31m
                },
                Listings = listings
            });
        }

        public Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(MarketAiReportCommand command, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<CompetitorShopAnalysisDto> AnalyzeCompetitorShopAsync(string shopNameOrUrl, int limit = 50, string authShopId = "53236321", CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IReadOnlyList<SeasonalTrendItem> GetSeasonalTrends(DateTime? referenceDate = null)
            => Array.Empty<SeasonalTrendItem>();
    }
}
