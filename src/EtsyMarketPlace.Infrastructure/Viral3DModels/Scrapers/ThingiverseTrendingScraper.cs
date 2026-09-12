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
using EtsyMarketPlace.Infrastructure.Viral3DModels.Repositories;
using EtsyMarketPlace.Infrastructure.Viral3DModels.Services;

public sealed class ThingiverseTrendingScraper : I3DModelPlatformScraper
{
    private readonly HttpClient _httpClient;

    public ModelPlatformType PlatformType => ModelPlatformType.Thingiverse;
    public string PlatformDisplayName => "Thingiverse";

    public ThingiverseTrendingScraper(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        }
    }

    public Task<IReadOnlyList<Trending3DModel>> GetTrendingModelsAsync(int page = 1, CancellationToken ct = default)
    {
        var models = Viral3DModelAtlasRepository.GetByPlatform(ModelPlatformType.Thingiverse);
        return Task.FromResult<IReadOnlyList<Trending3DModel>>(models);
    }

    public Task<IReadOnlyList<Trending3DModel>> SearchModelsAsync(string query, int page = 1, int pageSize = 30, CancellationToken ct = default)
    {
        var results = Viral3DModelAtlasRepository.Search(query, ModelPlatformType.Thingiverse);
        var pageItems = results.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<IReadOnlyList<Trending3DModel>>(pageItems);
    }

    public static List<Trending3DModel> GetCuratedThingiverseTrends()
    {
        return Viral3DModelAtlasRepository.GetByPlatform(ModelPlatformType.Thingiverse).ToList();
    }
}
