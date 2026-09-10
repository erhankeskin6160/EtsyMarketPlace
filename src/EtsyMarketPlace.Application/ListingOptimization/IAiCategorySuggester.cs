namespace EtsyMarketPlace.Application.ListingOptimization;

public interface IAiCategorySuggester
{
    Task<CategorySuggestionResult> SuggestCategoryAsync(
        string title,
        IReadOnlyList<string> imagePaths,
        string? description = null,
        string? tags = null,
        CancellationToken cancellationToken = default);
}
