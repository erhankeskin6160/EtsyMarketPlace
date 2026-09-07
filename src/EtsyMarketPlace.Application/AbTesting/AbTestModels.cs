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
    int InitialSales = 0);

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
