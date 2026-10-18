namespace EtsyMarketPlace.Application.SeasonalTrends;

using System.Linq;

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
)
{
    public string EventName => Title;
    public string PeakMonth => DateRangeDisplay;
    public string PrepStartDate => SeasonName;
    public int DaysLeftToPeak => Status.Contains("ZİRVE") ? 7 : (Status.Contains("HAZIRLIK") ? 35 : 90);
    public string StatusBadge => Status;
    public string CategoryFocus => KeyNiches != null && KeyNiches.Count > 0 ? string.Join(", ", KeyNiches.Take(3)) : SeasonName;
    public IReadOnlyList<string> WinningTags => WinningTagSuggestions;
    public decimal EstimatedTrafficMultiplier => Priority == 2 ? 4.2m : (Priority == 1 ? 3.5m : 2.8m);
}

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
