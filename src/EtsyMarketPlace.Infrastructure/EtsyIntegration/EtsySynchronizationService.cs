using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsySynchronizationService(IEtsyDataClient client, IEtsyIntegrationRepository repository) : IEtsySynchronizationService
{
    public async Task<EtsySyncResult> SynchronizeAsync(string shopId, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var end = endDate ?? DateTimeOffset.UtcNow;
        var cursor = await repository.GetLastCursorAsync(shopId, "financial", cancellationToken);
        var start = startDate ?? cursor?.AddMinutes(-5) ?? end.AddDays(-30);
        try
        {
            var payouts = await client.GetBankPayoutsAsync(shopId, start, end, cancellationToken);
            var transactions = await client.GetFinancialTransactionsAsync(shopId, start, end, cancellationToken);
            var alerts = await client.GetUnfulfilledCostAlertsAsync(shopId, cancellationToken);
            await repository.SavePayoutsAsync(payouts, cancellationToken);
            await repository.SaveTransactionsAsync(transactions, cancellationToken);
            await repository.SaveOrderAlertsAsync(alerts, cancellationToken);
            await repository.SaveSyncStateAsync(shopId, "financial", end, cancellationToken: cancellationToken);
            return new EtsySyncResult(shopId, startedAt, DateTimeOffset.UtcNow, payouts.Count, transactions.Count, alerts.Count, true, null);
        }
        catch (Exception ex)
        {
            await repository.SaveSyncStateAsync(shopId, "financial", cursor ?? start, ex.Message, cancellationToken);
            return new EtsySyncResult(shopId, startedAt, DateTimeOffset.UtcNow, 0, 0, 0, false, ex.Message);
        }
    }
}
