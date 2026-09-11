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

public sealed class MakerOnlineTrendingScraper : I3DModelPlatformScraper
{
    private readonly HttpClient _httpClient;

    public ModelPlatformType PlatformType => ModelPlatformType.MakerOnline;
    public string PlatformDisplayName => "MakerOnline (Anycubic)";

    public MakerOnlineTrendingScraper(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        SlicerClientProtocolFactory.ApplySlicerHeaders(_httpClient, ModelPlatformType.MakerOnline);
    }

    public Task<IReadOnlyList<Trending3DModel>> GetTrendingModelsAsync(int page = 1, CancellationToken ct = default)
    {
        var models = new List<Trending3DModel>
        {
            new()
            {
                ExternalId = "mo-3104",
                Platform = ModelPlatformType.MakerOnline,
                Title = "Bioluminescent Crystal Cave LED Lamp with Diffuser Shroud",
                Description = "Crystal geode lamp shell with internal hollow channel for USB LED fairy lights. Exquisite light dispersion with translucent PETG or resin.",
                AuthorName = "AuraSculpts",
                ModelPageUrl = "https://makeronline.com/en/search/model?keyword=Crystal+Cave+LED+Lamp",
                PrimaryImageUrl = "asset://crystal_lamp.jpg",
                GalleryImageUrls = ["asset://crystal_lamp.jpg"],
                Tags = ["Crystal Lamp", "LED Night Light", "Geode Decor", "Gothic Room Decor", "Lithophane"],
                Category = "Lighting & Lamps",
                Downloads24h = 1890,
                TotalDownloads = 8700,
                PrintsCount = 1850,
                LikesCount = 1320,
                License = ModelLicenseInfo.Commercial("Anycubic MakerOnline Commercial"),
                PrintSpecs = new PrintEstimation
                {
                    EstimatedPrintTimeMinutes = 310,
                    FilamentGrams = 135.0
                },
                EtsyCompetitionCount = 1,
                OpportunityScore = 97
            }
        };

        return Task.FromResult<IReadOnlyList<Trending3DModel>>(models);
    }
}
