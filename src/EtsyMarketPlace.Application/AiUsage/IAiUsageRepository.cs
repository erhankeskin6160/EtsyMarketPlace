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
    Task<string?> GetCachedPayloadAsync(string provider, string cacheKey, CancellationToken cancellationToken = default);
    Task SaveCachedPayloadAsync(string provider, string cacheKey, string category, string payloadJson, bool isFinalized, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetFinalizedDailyItemsAsync(string provider, string category, DateTimeOffset since, CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(string? provider = null, string? cacheKey = null, CancellationToken cancellationToken = default);
}
