namespace EtsyMarketPlace.Application.SeasonalTrends;

public sealed record SeasonalTrendItem(
    string Id,
    string Icon,
    string Title,
    string SeasonName,
    string DateRangeDisplay,
    string Status,
    string StatusColorHex,
    int Priority,
    IReadOnlyList<string> HighDemandKeywords,
    IReadOnlyList<string> WinningTagSuggestions,
    IReadOnlyList<string> KeyNiches,
    string ActionAdvice
);

public sealed record TrendNicheAnalysisResult(
    string Keyword,
    int TotalListingsSampled,
    decimal AveragePrice,
    decimal MinPrice,
    decimal MaxPrice,
    int AverageFavorites,
    int AverageViews,
    int DemandScore,
    int CompetitionScore,
    int OpportunityScore,
    IReadOnlyList<string> TopWinningTags,
    IReadOnlyList<string> HighOpportunityTitles,
    string Recommendation
);

public sealed record NicheListingSample(
    string Title,
    decimal Price,
    int Favorites,
    int Views,
    IReadOnlyList<string> Tags
);
