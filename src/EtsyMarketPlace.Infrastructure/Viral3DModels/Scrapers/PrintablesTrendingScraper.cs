namespace EtsyMarketPlace.Infrastructure.Viral3DModels.Scrapers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

using EtsyMarketPlace.Infrastructure.Viral3DModels.Protocols;

public sealed class PrintablesTrendingScraper : I3DModelPlatformScraper
{
    private readonly HttpClient _httpClient;

    public ModelPlatformType PlatformType => ModelPlatformType.Printables;
    public string PlatformDisplayName => "Printables (Prusa)";

    public PrintablesTrendingScraper(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        SlicerClientProtocolFactory.ApplySlicerHeaders(_httpClient, ModelPlatformType.Printables);
    }

    public Task<IReadOnlyList<Trending3DModel>> GetTrendingModelsAsync(int page = 1, CancellationToken ct = default)
    {
        var models = new List<Trending3DModel>
        {
            new()
            {
                ExternalId = "pr-51209",
                Platform = ModelPlatformType.Printables,
                Title = "Mechanical Iris Steampunk Coaster with Kinetic Opening Gear",
                Description = "Rotating gear-driven mechanical coaster. Placing a coffee mug or cup smoothly rotates the iris blades into an open mandala pattern.",
                AuthorName = "ClockworkGears",
                ModelPageUrl = "https://www.printables.com/model/51209",
                PrimaryImageUrl = "https://picsum.photos/seed/pr51209/600/450",
                GalleryImageUrls = ["https://picsum.photos/seed/pr51209_2/600/450"],
                Tags = ["Kinetic Coaster", "Steampunk", "Mechanical Toy", "Gamer Desk Decor", "Coffee Coaster"],
                Category = "Kitchen & Bar",
                Downloads24h = 3120,
                TotalDownloads = 22400,
                PrintsCount = 5100,
                LikesCount = 3890,
                License = ModelLicenseInfo.CreativeCommonsCommercial("CC-BY 4.0"),
                PrintSpecs = new PrintEstimation
                {
                    EstimatedPrintTimeMinutes = 190,
                    FilamentGrams = 85.0,
                    HasMultiColorProfile = true,
                    ColorCount = 2
                },
                EtsyCompetitionCount = 2,
                OpportunityScore = 94
            },
            new()
            {
                ExternalId = "pr-84310",
                Platform = ModelPlatformType.Printables,
                Title = "Modular Magnetic Controller & Headset Dual Hanger Dock",
                Description = "Universal heavy-duty wall and desk mount bracket for PS5, Xbox Series X, and Switch Pro controllers with stealth cable channel.",
                AuthorName = "ErgoRig",
                ModelPageUrl = "https://www.printables.com/model/84310",
                PrimaryImageUrl = "https://picsum.photos/seed/pr84310/600/450",
                GalleryImageUrls = ["https://picsum.photos/seed/pr84310_2/600/450"],
                Tags = ["Controller Stand", "PS5 Mount", "Xbox Controller Holder", "Gaming Setup", "Wall Mount"],
                Category = "Gaming Gear",
                Downloads24h = 2450,
                TotalDownloads = 16800,
                PrintsCount = 4200,
                LikesCount = 2950,
                License = ModelLicenseInfo.CreativeCommonsCommercial("CC-BY-SA"),
                PrintSpecs = new PrintEstimation
                {
                    EstimatedPrintTimeMinutes = 230,
                    FilamentGrams = 120.0
                },
                EtsyCompetitionCount = 5,
                OpportunityScore = 89
            }
        };

        return Task.FromResult<IReadOnlyList<Trending3DModel>>(models);
    }
}
