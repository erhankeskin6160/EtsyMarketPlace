namespace EtsyMarketPlace.Application.EtsyIntegration;

public interface IEtsyFinancialAnalysisService
{
    Task<FinancialAnalysisResult> AnalyzeAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);
}
