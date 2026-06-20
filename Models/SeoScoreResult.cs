namespace SimilarProductsWinForms.Models;

internal sealed record SeoScoreResult(
    int Score,
    int UsedTagCount,
    int DuplicateTagCount,
    int LongTailTagCount,
    List<string> Strengths,
    List<string> Warnings,
    List<string> Suggestions);
