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

public sealed class ThingiverseTrendingScraper : I3DModelPlatformScraper
{
    private readonly HttpClient _httpClient;

    public ModelPlatformType PlatformType => ModelPlatformType.Thingiverse;
    public string PlatformDisplayName => "Thingiverse";

    public ThingiverseTrendingScraper(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public Task<IReadOnlyList<Trending3DModel>> GetTrendingModelsAsync(int page = 1, CancellationToken ct = default)
    {
        var models = new List<Trending3DModel>
        {
            new()
            {
                ExternalId = "tv-64201",
                Platform = ModelPlatformType.Thingiverse,
                Title = "Vintage Cyberdeck Portable Terminal Enclosure for Raspberry Pi",
                Description = "Retro-futuristic cyberdeck chassis for Raspberry Pi 4/5 with mechanical keyboard cutout and 7-inch touchscreen bezel.",
                AuthorName = "RetroGrid",
                ModelPageUrl = "https://www.thingiverse.com/thing:64201",
                PrimaryImageUrl = "https://picsum.photos/seed/tv64201/600/450",
                GalleryImageUrls = ["https://picsum.photos/seed/tv64201_2/600/450"],
                Tags = ["Cyberdeck", "Raspberry Pi Case", "Sci-Fi Prop", "Geek Hardware", "Mechanical Keyboard"],
                Category = "Gadgets & Cases",
                Downloads24h = 1650,
                TotalDownloads = 19400,
                PrintsCount = 2900,
                LikesCount = 2100,
                License = ModelLicenseInfo.CreativeCommonsCommercial("CC-BY 3.0"),
                PrintSpecs = new PrintEstimation
                {
                    EstimatedPrintTimeMinutes = 480,
                    FilamentGrams = 240.0
                },
                EtsyCompetitionCount = 1,
                OpportunityScore = 96
            }
        };

        return Task.FromResult<IReadOnlyList<Trending3DModel>>(models);
    }
}
