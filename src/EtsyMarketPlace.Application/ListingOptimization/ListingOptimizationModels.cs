namespace EtsyMarketPlace.Application.ListingOptimization;

public sealed record ListingOptimizationInput(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    string TargetKeyword,
    string DescriptionStyle = "Storytelling");

public sealed record ListingOptimizationResult(
    int CurrentSeoScore,
    int OptimizedSeoScore,
    IReadOnlyList<string> TitleSuggestions,
    IReadOnlyList<string> TagSuggestions,
    IReadOnlyList<string> MaterialSuggestions,
    string DescriptionDraft,
    IReadOnlyList<string> MissingTerms,
    IReadOnlyList<string> RiskWarnings,
    IReadOnlyList<string> ActionChecklist,
    string ExecutedProvider = "Offline",
    string ExecutedModel = "RuleBased",
    bool IsFallback = false,
    string? FallbackReason = null,
    string? SeoCritique = null);

public sealed record ListingOptimizationHistoryEntry(
    long Id,
    DateTimeOffset CreatedAt,
    string ListingId,
    string ListingTitle,
    string TargetKeyword,
    int CurrentSeoScore,
    int OptimizedSeoScore,
    string SuggestedTitle,
    IReadOnlyList<string> SuggestedTags,
    string DescriptionDraft,
    IReadOnlyList<string> RiskWarnings);

public sealed record SaveListingOptimizationHistory(
    string ListingId,
    string ListingTitle,
    string TargetKeyword,
    ListingOptimizationResult Result);
