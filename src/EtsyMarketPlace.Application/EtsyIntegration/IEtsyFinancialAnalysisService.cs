using System;
using System.Threading;
using System.Threading.Tasks;

namespace EtsyMarketPlace.Application.EtsyIntegration;

public interface IEtsyFinancialAnalysisService
{
    Task<FinancialAnalysisResult> AnalyzeAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);

    Task<AiFinancialForecastResultDto> GenerateAiForecastAsync(
        AiFinancialForecastCommand command,
        CancellationToken cancellationToken = default);
}
