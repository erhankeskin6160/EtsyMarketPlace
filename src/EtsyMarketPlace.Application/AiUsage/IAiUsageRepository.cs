namespace EtsyMarketPlace.Application.AiUsage;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IAiUsageRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<AiUsageRecord> SaveUsageAsync(AiUsageRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiUsageRecord>> GetHistoryAsync(string? providerFilter = null, DateTimeOffset? since = null, int limit = 300, CancellationToken cancellationToken = default);
    Task<AiUsageSummaryStats> GetSummaryStatsAsync(string? providerFilter = null, DateTimeOffset? since = null, CancellationToken cancellationToken = default);
}
