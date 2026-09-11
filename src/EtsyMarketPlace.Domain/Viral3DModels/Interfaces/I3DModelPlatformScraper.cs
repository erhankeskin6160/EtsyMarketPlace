namespace EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;

public interface I3DModelPlatformScraper
{
    ModelPlatformType PlatformType { get; }
    string PlatformDisplayName { get; }
    Task<IReadOnlyList<Trending3DModel>> GetTrendingModelsAsync(int page = 1, CancellationToken ct = default);
}
