namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

public sealed class EfPostgreSqlAiUsageRepository : IAiUsageRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlAiUsageRepository> _logger;
    private static readonly ConcurrentDictionary<string, (string payload, DateTime expiresAt)> _memoryCache = new();

    public EfPostgreSqlAiUsageRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlAiUsageRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<AiUsageRecord> SaveUsageAsync(AiUsageRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));

        var entity = new AiUsageRecordEntity
        {
            CreatedAt = record.Timestamp.UtcDateTime,
            ModuleName = record.ModuleName ?? string.Empty,
            Provider = record.Provider ?? string.Empty,
            ModelName = record.ModelName ?? string.Empty,
            PromptTokens = record.PromptTokens,
            CompletionTokens = record.CompletionTokens,
            TotalTokens = record.TotalTokens,
            CostUsd = record.EstimatedCostUsd,
            CostTry = record.EstimatedCostTry,
            Status = record.Status ?? "Başarılı",
            Note = record.Note
        };

        _context.AiUsageHistory.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        record.Id = entity.Id;
        return record;
    }

    public async Task<IReadOnlyList<AiUsageRecord>> GetHistoryAsync(
        string? providerFilter = null,
        DateTimeOffset? since = null,
        int limit = 300,
        DateTimeOffset? until = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AiUsageHistory.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(providerFilter) && !providerFilter.Equals("Tümü", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Provider == providerFilter);
        }

        if (since.HasValue)
        {
            var sinceUtc = since.Value.UtcDateTime;
            query = query.Where(x => x.CreatedAt >= sinceUtc);
        }

        if (until.HasValue)
        {
            var untilUtc = until.Value.UtcDateTime;
            query = query.Where(x => x.CreatedAt <= untilUtc);
        }

        var entities = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new AiUsageRecord
        {
            Id = e.Id,
            Timestamp = new DateTimeOffset(e.CreatedAt, TimeSpan.Zero),
            ModuleName = e.ModuleName,
            Provider = e.Provider,
            ModelName = e.ModelName,
            PromptTokens = e.PromptTokens,
            CompletionTokens = e.CompletionTokens,
            TotalTokens = e.TotalTokens,
            EstimatedCostUsd = e.CostUsd,
            EstimatedCostTry = e.CostTry,
            Status = e.Status,
            Note = e.Note
        }).ToList();
    }

    public async Task<AiUsageSummaryStats> GetSummaryStatsAsync(
        string? providerFilter = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AiUsageHistory.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(providerFilter) && !providerFilter.Equals("Tümü", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Provider == providerFilter);
        }

        if (since.HasValue)
        {
            var sinceUtc = since.Value.UtcDateTime;
            query = query.Where(x => x.CreatedAt >= sinceUtc);
        }

        if (until.HasValue)
        {
            var untilUtc = until.Value.UtcDateTime;
            query = query.Where(x => x.CreatedAt <= untilUtc);
        }

        var list = await query.ToListAsync(cancellationToken);

        var stats = new AiUsageSummaryStats
        {
            ProviderFilter = string.IsNullOrWhiteSpace(providerFilter) ? "Tümü" : providerFilter,
            TotalRequests = list.Count,
            SuccessfulRequests = list.Count(x => x.Status == "Başarılı"),
            Blocked429Requests = list.Count(x => x.Status != null && x.Status.Contains("429")),
            ErrorRequests = list.Count(x => x.Status != null && (x.Status.Contains("Hata") || x.Status.Contains("Error"))),
            TotalPromptTokens = list.Sum(x => (long)x.PromptTokens),
            TotalCompletionTokens = list.Sum(x => (long)x.CompletionTokens),
            TotalTokens = list.Sum(x => (long)x.TotalTokens),
            TotalCostUsd = list.Sum(x => x.CostUsd),
            TotalCostTry = list.Sum(x => x.CostTry),
            CostByModel = list
                .GroupBy(x => string.IsNullOrWhiteSpace(x.ModelName) ? "Unknown" : x.ModelName)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.CostUsd)),
            TokensByProvider = list
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Provider) ? "Unknown" : x.Provider)
                .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.TotalTokens))
        };

        return stats;
    }

    public Task<string?> GetCachedPayloadAsync(string provider, string cacheKey, CancellationToken cancellationToken = default)
    {
        string key = $"{provider}:{cacheKey}";
        if (_memoryCache.TryGetValue(key, out var cached) && cached.expiresAt > DateTime.UtcNow)
        {
            return Task.FromResult<string?>(cached.payload);
        }
        return Task.FromResult<string?>(null);
    }

    public Task SaveCachedPayloadAsync(string provider, string cacheKey, string category, string payloadJson, bool isFinalized, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default)
    {
        string key = $"{provider}:{cacheKey}";
        var expiry = expiresAt?.UtcDateTime ?? DateTime.UtcNow.AddHours(24);
        _memoryCache[key] = (payloadJson, expiry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, string>> GetFinalizedDailyItemsAsync(string provider, string category, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, string> dict = new Dictionary<string, string>();
        return Task.FromResult(dict);
    }

    public Task InvalidateCacheAsync(string? provider = null, string? cacheKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider) && string.IsNullOrWhiteSpace(cacheKey))
        {
            _memoryCache.Clear();
        }
        else if (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(cacheKey))
        {
            _memoryCache.TryRemove($"{provider}:{cacheKey}", out _);
        }
        return Task.CompletedTask;
    }
}
