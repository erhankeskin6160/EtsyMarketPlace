namespace EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

using System;
using System.Collections.Generic;

public record ModelSearchQueryExpansion
{
    public string OriginalQuery { get; init; } = string.Empty;
    public string PrimaryEnglishTerm { get; init; } = string.Empty;
    public IReadOnlyList<string> SearchKeywords { get; init; } = [];
    public IReadOnlyList<string> ExpandedKeywords => SearchKeywords;
    public IReadOnlyList<string> Technical3DTags { get; init; } = [];
    public string TargetCategory { get; init; } = string.Empty;
    public bool CommercialIntentOnly { get; init; } = true;
    public string AiExplanation { get; init; } = string.Empty;

    public static ModelSearchQueryExpansion CreateDefault(string query)
    {
        return new ModelSearchQueryExpansion
        {
            OriginalQuery = query,
            PrimaryEnglishTerm = query,
            SearchKeywords = [query],
            Technical3DTags = ["3d print", "maker"],
            TargetCategory = "General",
            CommercialIntentOnly = true,
            AiExplanation = "Varsayılan doğrudan arama."
        };
    }
}
