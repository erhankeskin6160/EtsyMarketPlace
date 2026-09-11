namespace EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public interface I3DModelSnapshotRepository
{
    /// <summary>
    /// Persists time-series snapshots of discovered trending models for delta velocity calculation.
    /// </summary>
    Task SaveSnapshotsAsync(IEnumerable<Trending3DModel> models, CancellationToken ct = default);

    /// <summary>
    /// Computes delta downloads, hourly velocity and percentage growth based on historical snapshots.
    /// </summary>
    Task<ModelDeltaMetrics> GetDeltaMetricsAsync(
        string externalId,
        ModelPlatformType platform,
        int currentDownloads,
        int currentPrints,
        CancellationToken ct = default);
}
