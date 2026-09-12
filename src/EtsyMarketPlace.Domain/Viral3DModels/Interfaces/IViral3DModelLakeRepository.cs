namespace EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public interface IViral3DModelLakeRepository
{
    Task<int> SaveOrUpdateModelsAsync(IEnumerable<Trending3DModel> models, CancellationToken ct = default);
    Task<IReadOnlyList<Trending3DModel>> GetModelsAsync(int limit = 100, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<Trending3DModel>> SearchModelsAsync(
        string query,
        string? category = null,
        ModelPlatformType? platform = null,
        bool commercialOnly = false,
        int limit = 60,
        int offset = 0,
        CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<ModelDeltaMetrics> GetDeltaMetricsAsync(
        string externalId,
        ModelPlatformType platform,
        int currentDownloads,
        int currentPrints,
        CancellationToken ct = default);
}
