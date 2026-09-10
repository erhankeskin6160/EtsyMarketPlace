namespace EtsyMarketPlace.Application.ListingOptimization;

public sealed record TaxonomyCandidate(
    long TaxonomyId,
    string CategoryPath,
    int ConfidenceScore = 0);

public sealed class CategorySuggestionResult
{
    public long TaxonomyId { get; set; }
    public string CategoryPath { get; set; } = string.Empty;
    public int ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string ProviderUsed { get; set; } = string.Empty;
    public List<TaxonomyCandidate> Alternatives { get; set; } = [];

    public CategorySuggestionResult() { }

    public CategorySuggestionResult(
        long taxonomyId,
        string categoryPath,
        int confidenceScore,
        string reasoning,
        string providerUsed,
        IEnumerable<TaxonomyCandidate>? alternatives = null)
    {
        TaxonomyId = taxonomyId;
        CategoryPath = categoryPath;
        ConfidenceScore = confidenceScore;
        Reasoning = reasoning;
        ProviderUsed = providerUsed;
        if (alternatives != null)
        {
            Alternatives.AddRange(alternatives);
        }
    }

    public string DisplayText => TaxonomyId > 0
        ? $"{TaxonomyId} - {CategoryPath}"
        : CategoryPath;
}
