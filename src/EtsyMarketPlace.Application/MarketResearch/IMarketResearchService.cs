namespace EtsyMarketPlace.Application.MarketResearch;

using System.Threading;
using System.Threading.Tasks;

public interface IMarketResearchService
{
    Task<MarketSearchResponse> SearchMarketAsync(
        MarketSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(
        MarketAiReportCommand command,
        CancellationToken cancellationToken = default);
}
