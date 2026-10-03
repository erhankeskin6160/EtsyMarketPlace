using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.BatchQueue;

namespace EtsyMarketPlace.Api.Models;

public sealed record UpdateAbTestStatusRequest(AbTestStatus Status);

public sealed record BatchEnqueueRequest(IReadOnlyList<SaveBatchQueueItem> Items);

public sealed record BatchProcessRequest(
    string? OptimizedTitle,
    string? OptimizedDescription,
    IReadOnlyList<string>? OptimizedTags,
    int OverallScore,
    BatchQueueItemStatus Status);
