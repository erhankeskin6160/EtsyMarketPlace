namespace EtsyMarketPlace.Application.EtsyIntegration;

public interface IEtsyIntegrationRepository
{
    Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken cancellationToken = default);
    Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken cancellationToken = default);
    Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken cancellationToken = default);
    Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken cancellationToken = default);
    Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken cancellationToken = default);
    Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken cancellationToken = default);
    Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken cancellationToken = default);
    Task UpsertOrderCostAsync(string shopId, string orderId, decimal? productCost, decimal? shippingCost, decimal? packagingCost, string? notes, CancellationToken cancellationToken = default);
}
