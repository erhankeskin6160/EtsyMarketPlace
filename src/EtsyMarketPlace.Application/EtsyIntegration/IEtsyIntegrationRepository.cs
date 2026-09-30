namespace EtsyMarketPlace.Application.EtsyIntegration;

public interface IEtsyIntegrationRepository
{
    Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken cancellationToken = default);
    Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken = default);
    Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken cancellationToken = default);
    Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken cancellationToken = default);
    Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken cancellationToken = default);
}
