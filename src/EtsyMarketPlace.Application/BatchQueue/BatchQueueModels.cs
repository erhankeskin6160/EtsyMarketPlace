namespace EtsyMarketPlace.Application.BatchQueue;

/// <summary>
/// Status of an item in the batch optimization queue.
/// </summary>
public enum BatchQueueItemStatus
{
    Pending,
    Processing,
    Completed,
    RiskWarning,
    Failed,
    SyncedToEtsy,
    RolledBack,
}

/// <summary>
/// Represents a single listing queued for batch AI optimization and validation.
/// </summary>
public sealed record BatchQueueItem(
    long Id,
    DateTimeOffset CreatedAt,
    string ListingId,
    string OriginalTitle,
    string OriginalDescription,
    IReadOnlyList<string> OriginalTags,
    string TargetKeyword,
    string Category,
    BatchQueueItemStatus Status,
    int OverallScore,
    string OptimizedTitle,
    string OptimizedDescription,
    IReadOnlyList<string> OptimizedTags,
    IReadOnlyList<string> OptimizedMaterials,
    IReadOnlyList<string> RiskWarnings,
    IReadOnlyList<string> Issues,
    DateTimeOffset? ProcessedAt,
    string? ErrorMessage,
    bool IsSyncedToEtsy = false,
    DateTimeOffset? SyncedAt = null,
    long? AbTestExperimentId = null,
    string? AbTestStatus = null);

/// <summary>
/// DTO for adding items to the batch optimization queue.
/// </summary>
public sealed record SaveBatchQueueItem(
    string ListingId,
    string OriginalTitle,
    string OriginalDescription,
    IReadOnlyList<string> OriginalTags,
    string TargetKeyword,
    string Category = "");

/// <summary>
/// Live progress summary for batch queue processing.
/// </summary>
public sealed record BatchQueueSummary(
    int TotalItems,
    int PendingItems,
    int ProcessingItems,
    int CompletedItems,
    int RiskWarningItems,
    int FailedItems,
    double AverageScore,
    double ProgressPercent,
    string StatusMessage);

/// <summary>
/// Progress reporting during live Etsy synchronization.
/// </summary>
public sealed record BatchSyncProgress(
    int CurrentIndex,
    int TotalCount,
    string CurrentTitle,
    bool IsSuccess,
    string? Message);

/// <summary>
/// Summary result of a batch synchronization to Etsy.
/// </summary>
public sealed record BatchSyncResult(
    int TotalRequested,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> Errors);

