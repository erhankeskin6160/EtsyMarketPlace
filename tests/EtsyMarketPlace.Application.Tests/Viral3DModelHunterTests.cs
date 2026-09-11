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
}
