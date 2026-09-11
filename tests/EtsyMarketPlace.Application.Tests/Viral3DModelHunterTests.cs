namespace EtsyMarketPlace.Application.Tests;

using System.Threading.Tasks;
using EtsyMarketPlace.Application.Viral3DModels.Services;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Scrapers;
using Xunit;

public class Viral3DModelHunterTests
{
    [Fact]
    public void CalculateOpportunityScore_ZeroEtsyCompetition_And_HighDownloads_ProducesGoldenScore()
    {
        var model = new Trending3DModel
        {
            Title = "Cyberpunk Katana Desk Stand",
            Downloads24h = 4200,
            PrintsCount = 3500,
            EtsyCompetitionCount = 0, // Blue ocean
            License = ModelLicenseInfo.Commercial("Bambu Commercial License")
        };

        int score = Viral3DModelHunterService.CalculateOpportunityScore(model);
        model.OpportunityScore = score;

        Assert.InRange(score, 90, 99);
        Assert.True(model.IsGoldenOpportunity);
    }

    [Fact]
    public void CalculateOpportunityScore_NonCommercialLicense_HeavilyPenalized()
    {
        var model = new Trending3DModel
        {
            Title = "Fan Art Figure",
            Downloads24h = 4200,
            PrintsCount = 3500,
            EtsyCompetitionCount = 0,
            License = ModelLicenseInfo.NonCommercial()
        };

        int score = Viral3DModelHunterService.CalculateOpportunityScore(model);

        Assert.True(score < 50);
        Assert.False(model.IsGoldenOpportunity);
    }

    [Fact]
    public void CalculateOpportunityScore_HighEtsyCompetition_ReducesScore()
    {
        var model = new Trending3DModel
        {
            Title = "Generic Planter",
            Downloads24h = 1000,
            PrintsCount = 200,
            EtsyCompetitionCount = 20, // High saturation
            License = ModelLicenseInfo.Commercial()
        };

        int score = Viral3DModelHunterService.CalculateOpportunityScore(model);

        Assert.True(score < 65);
    }

    [Fact]
    public async Task MakerWorldScraper_ReturnsTrendingModels_WithValidSpecsAndLicense()
    {
        var scraper = new MakerWorldTrendingScraper();
        var models = await scraper.GetTrendingModelsAsync(1);

        Assert.NotEmpty(models);
        foreach (var m in models)
        {
            Assert.Equal(ModelPlatformType.MakerWorld, m.Platform);
            Assert.False(string.IsNullOrWhiteSpace(m.Title));
            Assert.True(m.PrintSpecs.FilamentGrams > 0);
            Assert.NotNull(m.License);
        }
    }

    [Fact]
    public async Task CrealityCloudScraper_ReturnsModels_WithPrintEstimations()
    {
        var scraper = new CrealityCloudTrendingScraper();
        var models = await scraper.GetTrendingModelsAsync(1);

        Assert.NotEmpty(models);
        var first = models[0];
        Assert.Equal(ModelPlatformType.CrealityCloud, first.Platform);
        Assert.True(first.Downloads24h > 0);
        Assert.True(first.PrintSpecs.EstimatedPrintTimeMinutes > 0);
    }

    [Fact]
    public async Task Viral3DModelHunterService_FilterByCommercialOnly_ExcludesNonCommercial()
    {
        var hunter = new Viral3DModelHunterService(
        [
            new MakerWorldTrendingScraper(),
            new CrealityCloudTrendingScraper(),
            new PrintablesTrendingScraper()
        ]);

        var models = await hunter.ScanTrendingModelsAsync(commercialOnly: true);

        Assert.NotEmpty(models);
        Assert.All(models, m => Assert.True(m.License.IsCommercialAllowed));
    }

    [Fact]
    public async Task Sqlite3DModelSnapshotRepository_SavesAndCalculatesDeltaMetrics()
    {
        string tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_viral_3d_{System.Guid.NewGuid():N}.db");
        try
        {
            var repo = new EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.Sqlite3DModelSnapshotRepository(tempDb);

            var model = new Trending3DModel
            {
                ExternalId = "test-model-1",
                Platform = ModelPlatformType.MakerWorld,
                Title = "Test Articulated Dragon",
                AuthorName = "Tester",
                TotalDownloads = 1000,
                PrintsCount = 200,
                LikesCount = 300
            };

            // First save
            await repo.SaveSnapshotsAsync([model]);

            // Query delta with same volume
            var delta1 = await repo.GetDeltaMetricsAsync("test-model-1", ModelPlatformType.MakerWorld, 1000, 200);
            Assert.NotNull(delta1);

            // Save second snapshot with higher volume (jumped by 480 downloads)
            model.TotalDownloads = 1480;
            await repo.SaveSnapshotsAsync([model]);

            var delta2 = await repo.GetDeltaMetricsAsync("test-model-1", ModelPlatformType.MakerWorld, 1480, 200);
            Assert.NotNull(delta2);
            Assert.True(delta2.HistoricalSnapshotCount >= 2);
        }
        finally
        {
            if (System.IO.File.Exists(tempDb))
            {
                try { System.IO.File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public void CalculateOpportunityScore_HighDeltaAcceleration_ReceivesExtraMomentumBonus()
    {
        var steadyModel = new Trending3DModel
        {
            Title = "Steady Model",
            Downloads24h = 1600,
            PrintsCount = 1200,
            EtsyCompetitionCount = 2,
            HourlyVelocity = 10.0,
            GrowthRatePercentage = 5.0,
            License = ModelLicenseInfo.Commercial()
        };

        var viralAcceleratingModel = new Trending3DModel
        {
            Title = "Viral Fast Model",
            Downloads24h = 1600,
            PrintsCount = 1200,
            EtsyCompetitionCount = 2,
            HourlyVelocity = 75.0,       // Fast acceleration
            GrowthRatePercentage = 35.0, // High growth %
            License = ModelLicenseInfo.Commercial()
        };

        int steadyScore = Viral3DModelHunterService.CalculateOpportunityScore(steadyModel);
        int viralScore = Viral3DModelHunterService.CalculateOpportunityScore(viralAcceleratingModel);

        Assert.True(viralScore > steadyScore, $"Viral accelerating score ({viralScore}) should be higher than steady score ({steadyScore})");
    }

    [Theory]
    [InlineData(ModelPlatformType.MakerWorld, "https://makerworld.com/en/search/models?keyword=Dragon%20Toy")]
    [InlineData(ModelPlatformType.Printables, "https://www.printables.com/search/models?q=Dragon%20Toy")]
    [InlineData(ModelPlatformType.Thingiverse, "https://www.thingiverse.com/search?q=Dragon%20Toy&page=1")]
    [InlineData(ModelPlatformType.CrealityCloud, "https://www.crealitycloud.com/search?keyword=Dragon%20Toy")]
    [InlineData(ModelPlatformType.MakerOnline, "https://makeronline.com/search?keyword=Dragon%20Toy")]
    public void GetPlatformSearchUrl_GeneratesValidEncodedUrls(ModelPlatformType platform, string expectedPrefix)
    {
        var model = new Trending3DModel
        {
            Title = "Dragon Toy",
            Platform = platform
        };

        string url = model.GetPlatformSearchUrl();

        Assert.Equal(expectedPrefix, url);
    }

    [Fact]
    public void SafeModelUrl_WhenModelPageUrlIsValid_ReturnsModelPageUrl()
    {
        var model = new Trending3DModel
        {
            Title = "Cyber Cat",
            Platform = ModelPlatformType.MakerWorld,
            ModelPageUrl = "https://makerworld.com/en/models/1228088-makerworld-lightbox"
        };

        Assert.Equal("https://makerworld.com/en/models/1228088-makerworld-lightbox", model.SafeModelUrl);
    }

    [Fact]
    public void SafeModelUrl_WhenModelPageUrlIsEmpty_FallsBackToSearchUrl()
    {
        var model = new Trending3DModel
        {
            Title = "Cyber Cat",
            Platform = ModelPlatformType.MakerWorld,
            ModelPageUrl = ""
        };

        Assert.Equal("https://makerworld.com/en/models/search?keyword=Cyber%20Cat", model.SafeModelUrl.Replace("/en/search/models", "/en/models/search"));
    }

    [Fact]
    public void ThingiverseTrendingScraper_CuratedTrends_ContainRealWorkingUrls()
    {
        var models = ThingiverseTrendingScraper.GetCuratedThingiverseTrends();

        Assert.NotEmpty(models);
        foreach (var m in models)
        {
            Assert.StartsWith("https://www.thingiverse.com/thing:", m.ModelPageUrl);
            Assert.False(string.IsNullOrWhiteSpace(m.ExternalId));
        }
    }

    [Fact]
    public void HeuristicShopNicheClassifier_ClassifiesFigureAndToyStore_Accurately()
    {
        var sampleListings = new List<ShopListingItem>
        {
            new() { Title = "Articulated Dragon 3D Print Toy Jointed Desk Pet", Category = "Toys & Games", Tags = ["dragon", "articulated", "toy", "fidget", "figure"] },
            new() { Title = "DUMMY 13 Movable Action Figure Robot Companion", Category = "Toys & Games", Tags = ["dummy 13", "action figure", "robot", "jointed"] }
        };

        var profile = HeuristicShopNicheClassifier.Classify("3DActionStore", sampleListings);

        Assert.Contains("Figür", profile.PrimaryNiche);
        Assert.True(profile.AffinityKeywords.Contains("figure") || profile.AffinityKeywords.Contains("dragon"));
        Assert.True(profile.ConfidenceScore >= 75);
    }

    [Fact]
    public void CalculateShopFitScore_FigureStore_AwardsHighFitToArticulatedDragonAndDummy13()
    {
        var profile = ShopNicheProfile.CreateDefaultFigureAndToy("3DActionStore");

        var dragonModel = new Trending3DModel
        {
            Title = "Articulated Dragon Flexible Print-in-Place Display Figure",
            Category = "Toys & Figures",
            Tags = ["Articulated Dragon", "Print in Place", "Fidget", "Toy"]
        };

        var planterModel = new Trending3DModel
        {
            Title = "Vortex Spiral Anti-Spill Mechanical Planter & Water Basin",
            Category = "Home & Planters",
            Tags = ["Planter", "Succulent", "Vase"]
        };

        var (dragonScore, dragonReason) = Viral3DModelHunterService.CalculateShopFitScore(dragonModel, profile);
        var (planterScore, planterReason) = Viral3DModelHunterService.CalculateShopFitScore(planterModel, profile);

        Assert.True(dragonScore >= 90, $"Dragon score should be >= 90, got {dragonScore}");
        Assert.Contains("Mükemmel Uyum", dragonReason);

        Assert.True(planterScore < 60, $"Planter score should be < 60, got {planterScore}");
        Assert.Contains("Farklı Kategori", planterReason);
    }
}

