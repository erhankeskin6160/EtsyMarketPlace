namespace EtsyMarketPlace.Application.AbTesting;

/// <summary>
/// Status of an A/B test experiment.
/// </summary>
public enum AbTestStatus
{
    Active,
    Completed,
    Cancelled,
}

/// <summary>
/// Represents an A/B test experiment comparing Variant A (original) vs Variant B (new/AI optimized).
/// </summary>
public sealed record ListingAbTestExperiment(
    long Id,
    DateTimeOffset CreatedAt,
    string ListingId,
    string ListingTitle,
    string ExperimentName,
    string VariantA_Title,
    string VariantB_Title,
    IReadOnlyList<string> VariantA_Tags,
    IReadOnlyList<string> VariantB_Tags,
    string VariantA_Description,
    string VariantB_Description,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    AbTestStatus Status,
    int BeforeViews,
    int BeforeFavorites,
    int BeforeSales,
    int AfterViews,
    int AfterFavorites,
    int AfterSales);

/// <summary>
/// DTO for creating a new A/B test experiment.
/// </summary>
public sealed record SaveAbTestExperiment(
    string ListingId,
    string ListingTitle,
    string ExperimentName,
    string VariantA_Title,
    string VariantB_Title,
    IReadOnlyList<string> VariantA_Tags,
    IReadOnlyList<string> VariantB_Tags,
    string VariantA_Description,
    string VariantB_Description,
    int InitialViews = 0,
    int InitialFavorites = 0,
    int InitialSales = 0,
    DateTimeOffset? EndDate = null);

/// <summary>
/// DTO for updating performance metrics of an A/B test experiment.
/// </summary>
public sealed record UpdateAbTestMetrics(
    long ExperimentId,
    int AfterViews,
    int AfterFavorites,
    int AfterSales,
    bool CompleteExperiment = false);

/// <summary>
/// Options for launching a batch of A/B experiments.
/// </summary>
public sealed record BulkAbTestLaunchOptions(
    int DurationDays = 14,
    string ExperimentPrefix = "[Parti]",
    bool AutoDeployVariantBToEtsy = false,
    bool EnableSafetyGuardrail = true);

/// <summary>
/// Request to launch an A/B test from a batch queue item.
/// </summary>
public sealed record BulkAbTestItemRequest(
    long BatchQueueItemId,
    string ListingId,
    string OriginalTitle,
    string OriginalDescription,
    IReadOnlyList<string> OriginalTags,
    string OptimizedTitle,
    string OptimizedDescription,
    IReadOnlyList<string> OptimizedTags,
    int CurrentViews = 0,
    int CurrentFavorites = 0,
    int CurrentSales = 0);

/// <summary>
/// Progress reporting for bulk A/B test launch.
/// </summary>
public sealed record BulkAbTestLaunchProgress(
    int CurrentIndex,
    int TotalCount,
    string ListingTitle,
    bool Success,
    string? ErrorMessage);

/// <summary>
/// Result summary of launching a batch of A/B tests.
/// </summary>
public sealed record BulkAbTestLaunchResult(
    int TotalRequested,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<ListingAbTestExperiment> CreatedExperiments,
    IReadOnlyList<string> Errors);

/// <summary>
/// Early warning alert for abnormal performance drops during an active A/B test.
/// </summary>
public sealed record AbTestSafetyAlert(
    long ExperimentId,
    string ListingId,
    string ListingTitle,
    double DropPercentage,
    string WarningMessage,
    bool RecommendationRollback);

/// <summary>
/// Performance impact report calculating changes and determining winner variant.
/// </summary>
public sealed record AbTestImpactReport(
    long ExperimentId,
    string ExperimentName,
    string ListingTitle,
    AbTestStatus Status,
    int BeforeViews,
    int AfterViews,
    double ViewsChangePercent,
    int BeforeFavorites,
    int AfterFavorites,
    double FavoritesChangePercent,
    int BeforeSales,
    int AfterSales,
    double SalesChangePercent,
    string WinnerVariant,
    string ImpactSummary,
    IReadOnlyList<string> KeyTakeaways);
