namespace EtsyMarketPlace.Infrastructure.Viral3DModels.Scrapers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Protocols;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories;

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
        var models = Viral3DModelAtlasRepository.GetByPlatform(ModelPlatformType.MakerOnline);
        return Task.FromResult<IReadOnlyList<Trending3DModel>>(models);
    }

    public Task<IReadOnlyList<Trending3DModel>> SearchModelsAsync(string query, int page = 1, int pageSize = 30, CancellationToken ct = default)
    {
        var results = Viral3DModelAtlasRepository.Search(query, ModelPlatformType.MakerOnline);
        var pageItems = results.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<IReadOnlyList<Trending3DModel>>(pageItems);
    }
}
