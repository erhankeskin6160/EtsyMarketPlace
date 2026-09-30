namespace EtsyMarketPlace.Application.EtsyIntegration;

public interface IEtsyReportingService
{
    Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default);
    Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken cancellationToken = default);
}
