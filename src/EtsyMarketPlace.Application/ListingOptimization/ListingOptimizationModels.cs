namespace EtsyMarketPlace.Application.ListingOptimization;

public sealed record ListingOptimizationInput(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    string TargetKeyword);

public sealed record ListingOptimizationResult(
    int CurrentSeoScore,
    int OptimizedSeoScore,
    IReadOnlyList<string> TitleSuggestions,
    IReadOnlyList<string> TagSuggestions,
    string DescriptionDraft,
    IReadOnlyList<string> MissingTerms,
    IReadOnlyList<string> RiskWarnings,
    IReadOnlyList<string> ActionChecklist);
