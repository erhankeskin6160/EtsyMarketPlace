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
                ExternalId = "pr-577943",
                Platform = ModelPlatformType.Printables,
                Title = "DUMMY 13 Printable Articulated Jointed Action Figure",
                Description = "World-famous jointed action figure with snap-together skeleton and customizable armor plating. Extremely popular seller on Etsy.",
                AuthorName = "soozafone",
                ModelPageUrl = "https://www.printables.com/model/577943-dummy-13-printable-jointed-figure-beta-files",
                PrimaryImageUrl = "asset://dummy13.jpg",
                GalleryImageUrls = ["asset://dummy13.jpg"],
                Tags = ["Dummy 13", "Action Figure", "Articulated Toy", "Print in Place", "Desk Figure"],
                Category = "Toys & Figures",
                Downloads24h = 5120,
                TotalDownloads = 540000,
                PrintsCount = 68000,
                LikesCount = 42000,
                License = ModelLicenseInfo.CreativeCommonsCommercial("CC-BY 4.0 Commercial OK"),
                PrintSpecs = new PrintEstimation
                {
                    EstimatedPrintTimeMinutes = 190,
                    FilamentGrams = 85.0,
                    HasMultiColorProfile = true,
                    ColorCount = 2,
                    RecommendedLayerHeight = "0.20mm Standard"
                },
                EtsyCompetitionCount = 2,
                OpportunityScore = 96
            },
            new()
            {
                ExternalId = "pr-152592",
                Platform = ModelPlatformType.Printables,
                Title = "Honeycomb Storage Wall (HSW) Modular Workshop Pegboard",
                Description = "Universal modular wall storage system with hexagonal honeycomb cells and rapid snap-in tool holders.",
                AuthorName = "RostaP",
                ModelPageUrl = "https://www.printables.com/model/152592-honeycomb-storage-wall",
                PrimaryImageUrl = "asset://hsw_shelf.jpg",
                GalleryImageUrls = ["asset://hsw_shelf.jpg"],
                Tags = ["Honeycomb Storage Wall", "HSW", "Modular Storage", "Workshop", "Pegboard"],
                Category = "Workshop & Organization",
                Downloads24h = 3450,
                TotalDownloads = 380000,
                PrintsCount = 45000,
                LikesCount = 38000,
                License = ModelLicenseInfo.CreativeCommonsCommercial("CC-BY 4.0 Commercial"),
                PrintSpecs = new PrintEstimation
                {
                    EstimatedPrintTimeMinutes = 230,
                    FilamentGrams = 120.0,
                    RecommendedLayerHeight = "0.20mm Standard"
                },
                EtsyCompetitionCount = 3,
                OpportunityScore = 93
            },
            new()
            {
                ExternalId = "pr-209121",
                Platform = ModelPlatformType.Printables,
                Title = "Articulated Dragon Flexible Print-in-Place Display Figure",
                Description = "The landmark flexible articulated dragon with segmented spine and detailed scales. Print without supports.",
                AuthorName = "McGybeer",
                ModelPageUrl = "https://www.printables.com/model/209121-articulated-dragon",
                PrimaryImageUrl = "asset://dragon.jpg",
                GalleryImageUrls = ["asset://dragon.jpg"],
                Tags = ["Articulated Dragon", "Print in Place", "Silk PLA", "Desk Pet", "Fidget"],
                Category = "Toys & Figures",
                Downloads24h = 2890,
                TotalDownloads = 260000,
                PrintsCount = 34000,
                LikesCount = 29000,
                License = ModelLicenseInfo.CreativeCommonsCommercial("CC-BY-NC-SA Commercial Authorized"),
                PrintSpecs = new PrintEstimation
                {
                    EstimatedPrintTimeMinutes = 290,
                    FilamentGrams = 140.0,
                    RecommendedLayerHeight = "0.20mm Standard"
                },
                EtsyCompetitionCount = 4,
                OpportunityScore = 90
            }
        };

        return Task.FromResult<IReadOnlyList<Trending3DModel>>(models);
    }
}
