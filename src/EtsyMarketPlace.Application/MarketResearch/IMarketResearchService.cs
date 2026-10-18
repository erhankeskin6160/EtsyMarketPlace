namespace EtsyMarketPlace.Application.MarketResearch;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.SeasonalTrends;

public interface IMarketResearchService
{
    Task<MarketSearchResponse> SearchMarketAsync(
        MarketSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(
        MarketAiReportCommand command,
        CancellationToken cancellationToken = default);

    Task<CompetitorShopAnalysisDto> AnalyzeCompetitorShopAsync(
        string shopNameOrUrl,
        int limit = 50,
        string authShopId = "53236321",
        CancellationToken cancellationToken = default);

    IReadOnlyList<SeasonalTrendItem> GetSeasonalTrends(DateTime? referenceDate = null);
}
