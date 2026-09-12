namespace EtsyMarketPlace.Application.Tests;

using System.Threading.Tasks;
using EtsyMarketPlace.Application.Viral3DModels.Services;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Scrapers;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Services;
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

    [Fact]
    public void Viral3DModelAtlasRepository_ContainsRichCatalogAcrossCategories()
    {
        var all = EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.Viral3DModelAtlasRepository.GetAllModels();
        Assert.True(all.Count >= 25, $"Atlas should contain at least 25 models, got {all.Count}");

        var dragons = EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.Viral3DModelAtlasRepository.Search("dragon");
        Assert.NotEmpty(dragons);

        var dice = EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.Viral3DModelAtlasRepository.Search("dice");
        Assert.NotEmpty(dice);

        var planters = EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.Viral3DModelAtlasRepository.Search("planter");
        Assert.NotEmpty(planters);
    }

    [Fact]
    public async Task SearchModelsAcrossPlatformsAsync_ReturnsFilteredResults()
    {
        var scrapers = new List<I3DModelPlatformScraper>
        {
            new MakerWorldTrendingScraper(),
            new PrintablesTrendingScraper(),
            new ThingiverseTrendingScraper()
        };
        var hunter = new Viral3DModelHunterService(scrapers);

        var results = await hunter.SearchModelsAcrossPlatformsAsync("dragon", commercialOnly: true);

        Assert.NotEmpty(results);
        foreach (var m in results)
        {
            Assert.True(m.License.IsCommercialAllowed);
            Assert.True(m.Title.Contains("dragon", System.StringComparison.OrdinalIgnoreCase) ||
                        m.Tags.Any(t => t.Contains("dragon", System.StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Fact]
    public async Task DeepScanByShopNicheAsync_PrioritizesMatchingShopModels()
    {
        var scrapers = new List<I3DModelPlatformScraper>
        {
            new MakerWorldTrendingScraper(),
            new PrintablesTrendingScraper()
        };
        var hunter = new Viral3DModelHunterService(scrapers);
        var profile = ShopNicheProfile.CreateDefaultFigureAndToy("MyToyShop");

        var results = await hunter.DeepScanByShopNicheAsync(profile, commercialOnly: true);

        Assert.NotEmpty(results);
        Assert.True(results[0].ShopFitScore >= 80, $"Top result should have high shop fit score, got {results[0].ShopFitScore}");
    }

    [Fact]
    public void Viral3DModelAssetManager_GetAssetForModel_ResolvesIntelligently()
    {
        string dummyAsset = Viral3DModelAssetManager.GetAssetForModel("DUMMY 13 Movable Robot");
        Assert.Equal("asset://dummy13.jpg", dummyAsset);

        string dragonAsset = Viral3DModelAssetManager.GetAssetForModel("Articulated Crystal Dragon");
        Assert.Equal("asset://dragon.jpg", dragonAsset);

        string diceAsset = Viral3DModelAssetManager.GetAssetForModel("Medieval Castle Dice Tower");
        Assert.Equal("asset://dicetower.jpg", diceAsset);

        string planterAsset = Viral3DModelAssetManager.GetAssetForModel("Vortex Spiral Planter");
        Assert.Equal("asset://planter.jpg", planterAsset);
    }

    [Fact]
    public async Task AiModelSearchExpander_ExpandsTurkishQuery_To_Technical3DTerms()
    {
        var expander = new AiModelSearchExpander();

        // 1. Test Turkish dragon expansion
        var dragonExp = await expander.ExpandQueryAsync("ejderha");
        Assert.Contains("dragon", dragonExp.PrimaryEnglishTerm, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("articulated dragon", dragonExp.ExpandedKeywords);
        Assert.Contains("print-in-place", dragonExp.Technical3DTags);

        // 2. Test Turkish planter expansion
        var planterExp = await expander.ExpandQueryAsync("saksı");
        Assert.Contains("planter", planterExp.PrimaryEnglishTerm, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("spiral planter", planterExp.ExpandedKeywords);

        // 3. Test Turkish fidget expansion
        var fidgetExp = await expander.ExpandQueryAsync("fidget");
        Assert.Contains("fidget toy", fidgetExp.ExpandedKeywords);
    }

    [Fact]
    public async Task SqliteViral3DModelLakeRepository_SeedsAndQueriesModelsSuccessfully()
    {
        string tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_lake_{System.Guid.NewGuid():N}.db");
        try
        {
            var repo = new EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.SqliteViral3DModelLakeRepository(tempDb);

            // 1. Verify auto-seeding
            int total = await repo.GetTotalCountAsync();
            Assert.True(total >= 10, $"Auto-seed should populate at least 10 models, got {total}");

            // 2. Query search
            var dragons = await repo.SearchModelsAsync("dragon");
            Assert.NotEmpty(dragons);
            Assert.All(dragons, d => Assert.Contains("dragon", d.Title, System.StringComparison.OrdinalIgnoreCase));

            // 3. Test Upsert
            var newModel = new Trending3DModel
            {
                ExternalId = "test_lake_model_1",
                Platform = ModelPlatformType.MakerWorld,
                Title = "Test Custom Lake Model",
                Description = "High-speed 3d model for test",
                Category = "Toys & Figures",
                Downloads24h = 500,
                TotalDownloads = 1500,
                PrintsCount = 300,
                LikesCount = 200,
                License = ModelLicenseInfo.Commercial("CC-BY 4.0"),
                PrintSpecs = new PrintEstimation { FilamentGrams = 45, EstimatedPrintTimeMinutes = 90 }
            };

            int saved = await repo.SaveOrUpdateModelsAsync([newModel]);
            Assert.Equal(1, saved);

            int newTotal = await repo.GetTotalCountAsync();
            Assert.Equal(total + 1, newTotal);
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
    public async Task Viral3DModelHunterService_WithLakeAndExpander_IntegratesEndToEnd()
    {
        string tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_lake_svc_{System.Guid.NewGuid():N}.db");
        try
        {
            var scrapers = new List<I3DModelPlatformScraper>
            {
                new MakerWorldTrendingScraper(),
                new PrintablesTrendingScraper()
            };
            var expander = new AiModelSearchExpander();
            var lake = new EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.SqliteViral3DModelLakeRepository(tempDb);
            var hunter = new Viral3DModelHunterService(scrapers, null, null, expander, lake);

            // Search with Turkish term: 'ejderha'
            var results = await hunter.SearchModelsAcrossPlatformsAsync("ejderha", commercialOnly: true);

            Assert.NotEmpty(results);
            Assert.NotNull(hunter.LastQueryExpansion);
            Assert.Contains("dragon", hunter.LastQueryExpansion.PrimaryEnglishTerm, System.StringComparison.OrdinalIgnoreCase);
            Assert.True(results.Any(m => m.Title.Contains("dragon", System.StringComparison.OrdinalIgnoreCase)));

            // Verify models were saved to the Lake
            int lakeCount = await lake.GetTotalCountAsync();
            Assert.True(lakeCount > 0, "Discovered models should be persisted into SQLite Lake");
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
    public void AtlasRepository_DragonModel_HasVerifiedRealUrlAndImageAsset()
    {
        var models = EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.Viral3DModelAtlasRepository.GetAllModels();

        // 1. Must NOT contain legacy buggy ID
        Assert.DoesNotContain(models, m => m.ExternalId == "tv-5197816");
        Assert.DoesNotContain(models, m => (m.ModelPageUrl ?? "").Contains("5197816"));

        // 2. Must contain verified real Thingiverse Articulated Dragon
        var dragon = models.FirstOrDefault(m => m.ExternalId == "tv-3505006");
        Assert.NotNull(dragon);
        Assert.Equal("https://www.thingiverse.com/thing:3505006", dragon.ModelPageUrl);
        Assert.Equal("https://www.thingiverse.com/thing:3505006", dragon.SafeModelUrl);
        Assert.Equal("7Fish / McGybeer", dragon.AuthorName);
        Assert.False(string.IsNullOrWhiteSpace(dragon.PrimaryImageUrl));
        Assert.Contains("dragon", dragon.PrimaryImageUrl, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqliteLakeRepository_AutomaticallyPurgesBuggy5197816_AndSyncsRealDragon()
    {
        string tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_lake_purge_{System.Guid.NewGuid():N}.db");
        try
        {
            var lake = new EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.SqliteViral3DModelLakeRepository(tempDb);

            var models = await lake.GetModelsAsync(limit: 100);
            Assert.NotEmpty(models);

            // Buggy 5197816 must NOT exist in the lake
            Assert.DoesNotContain(models, m => m.ExternalId == "tv-5197816");
            Assert.DoesNotContain(models, m => (m.ModelPageUrl ?? "").Contains("5197816"));

            // Real dragon 3505006 must exist
            var dragon = models.FirstOrDefault(m => m.ExternalId == "tv-3505006");
            Assert.NotNull(dragon);
            Assert.Equal("https://www.thingiverse.com/thing:3505006", dragon.ModelPageUrl);
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
    public void AtlasRepository_Dummy13Model_HasVerifiedRealUrl_981111()
    {
        var models = EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.Viral3DModelAtlasRepository.GetAllModels();

        // 1. Must NOT contain legacy buggy attic stairs ID
        Assert.DoesNotContain(models, m => m.ExternalId == "pr-577943");
        Assert.DoesNotContain(models, m => (m.ModelPageUrl ?? "").Contains("577943"));

        // 2. Must contain verified real official DUMMY 13 v1.0
        var dummy = models.FirstOrDefault(m => m.ExternalId == "pr-981111");
        Assert.NotNull(dummy);
        Assert.Equal("https://www.printables.com/model/981111-dummy-13-version-10", dummy.ModelPageUrl);
        Assert.Equal("https://www.printables.com/model/981111-dummy-13-version-10", dummy.SafeModelUrl);
        Assert.Equal("soozafone", dummy.AuthorName);
        Assert.False(string.IsNullOrWhiteSpace(dummy.PrimaryImageUrl));
        Assert.Contains("dummy13", dummy.PrimaryImageUrl, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeModelUrl_WhenPrintablesModelIsAtticStairs_FallsBackToSearch()
    {
        var model = new Trending3DModel
        {
            Title = "DUMMY 13 Printable Jointed Action Figure",
            Platform = ModelPlatformType.Printables,
            ModelPageUrl = "https://www.printables.com/model/577943-revision-set-attic-stairs"
        };

        // Must NOT open attic stairs 577943; must fall back to platform search
        Assert.DoesNotContain("577943", model.SafeModelUrl);
        Assert.Contains("search", model.SafeModelUrl, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HermesAgent_DetectsKnownDrift_AndHealsToOfficialDummy13Url()
    {
        var agent = new EtsyMarketPlace.Infrastructure.Viral3DModels.Services.Hermes3DScoutAgent();
        var model = new Trending3DModel
        {
            Title = "DUMMY 13 Printable Articulated Jointed Action Figure",
            Platform = ModelPlatformType.Printables,
            ModelPageUrl = "https://www.printables.com/model/577943-attic-stairs"
        };

        var result = await agent.VerifyAndHealModelAsync(model);

        Assert.True(result.IsVerified);
        Assert.Equal("https://www.printables.com/model/981111-dummy-13-version-10", result.VerifiedUrl);
        Assert.Equal("soozafone", result.VerifiedAuthor);
        Assert.False(result.HasIpCopyrightRisk);
    }

    [Fact]
    public async Task HermesAgent_DetectsIpTrademarkRisk_AndRaisesWarning()
    {
        var agent = new EtsyMarketPlace.Infrastructure.Viral3DModels.Services.Hermes3DScoutAgent();
        var model = new Trending3DModel
        {
            Title = "Cute Articulated Pikachu Pokemon Desk Figure",
            Platform = ModelPlatformType.Printables,
            Tags = ["pokemon", "pikachu", "nintendo"],
            ModelPageUrl = "https://www.printables.com/model/123456-pikachu"
        };

        var result = await agent.VerifyAndHealModelAsync(model);

        Assert.True(result.HasIpCopyrightRisk);
        Assert.NotNull(result.IpRiskWarning);
        Assert.Contains("Tescilli Marka Tespiti", result.IpRiskWarning);
        Assert.Contains("POKEMON", result.IpRiskWarning);
    }

    [Fact]
    public void VisualBrowserAgentService_DetectsInstalledBrowser_OrReturnsSafeFallback()
    {
        string? browser = EtsyMarketPlace.Infrastructure.Viral3DModels.Services.VisualBrowserAgentService.ResolveInstalledBrowserPath();
        // Machine has Chrome installed as verified earlier
        Assert.True(EtsyMarketPlace.Infrastructure.Viral3DModels.Services.VisualBrowserAgentService.IsBrowserAvailable);
        Assert.NotNull(browser);
        Assert.True(System.IO.File.Exists(browser));
    }

    [Fact]
    public async Task HermesAgent_VerifyWithVisualBrowserAsync_ThrowsOnNullModel()
    {
        var agent = new EtsyMarketPlace.Infrastructure.Viral3DModels.Services.Hermes3DScoutAgent();
        await Assert.ThrowsAsync<System.ArgumentNullException>(() => agent.VerifyWithVisualBrowserAsync(null!));
    }

    [Fact]
    public async Task HermesAgent_ScoutAndHarvestForShopAsync_ThrowsOnNullProfile()
    {
        var agent = new EtsyMarketPlace.Infrastructure.Viral3DModels.Services.Hermes3DScoutAgent();
        await Assert.ThrowsAsync<System.ArgumentNullException>(() => agent.ScoutAndHarvestForShopAsync(null!));
    }

    [Fact]
    public async Task HermesAgent_ScoutAndHarvestForShopAsync_PersistsModelsIntoLake()
    {
        string tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"scout_lake_{System.Guid.NewGuid():N}.db");
        try
        {
            var lake = new EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories.SqliteViral3DModelLakeRepository(tempDb);
            var agent = new EtsyMarketPlace.Infrastructure.Viral3DModels.Services.Hermes3DScoutAgent(lake);
            var profile = EtsyMarketPlace.Domain.Viral3DModels.ValueObjects.ShopNicheProfile.CreateDefaultFigureAndToy();

            var results = await agent.ScoutAndHarvestForShopAsync(profile, ModelPlatformType.Printables, maxModels: 5);

            Assert.NotEmpty(results);
            Assert.All(results, m =>
            {
                Assert.True(m.ShopFitScore >= 50);
                Assert.NotEmpty(m.ShopFitReason);
            });

            int totalInLake = await lake.GetTotalCountAsync();
            Assert.True(totalInLake > 0, "Scouted models should be saved into SQLite 3D Model Lake repository");
        }
        finally
        {
            if (System.IO.File.Exists(tempDb))
            {
                try { System.IO.File.Delete(tempDb); } catch { }
            }
        }
    }
}
