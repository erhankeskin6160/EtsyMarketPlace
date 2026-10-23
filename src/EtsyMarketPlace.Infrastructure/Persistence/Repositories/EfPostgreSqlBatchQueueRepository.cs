using EtsyMarketPlace.Domain.Common;
namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

public sealed class EfPostgreSqlBatchQueueRepository : IBatchQueueRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlBatchQueueRepository> _logger;

    public EfPostgreSqlBatchQueueRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlBatchQueueRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<IReadOnlyList<BatchQueueItem>> EnqueueBatchAsync(
        IReadOnlyList<SaveBatchQueueItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0) return [];

        var entities = items.Select(item => new BatchQueueItemEntity
        {
            CreatedAt = DateTime.UtcNow,
            ListingId = item.ListingId ?? string.Empty,
            OriginalTitle = item.OriginalTitle ?? string.Empty,
            OriginalDescription = item.OriginalDescription ?? string.Empty,
            OriginalTags = JsonSerializer.Serialize(item.OriginalTags ?? []),
            TargetKeyword = item.TargetKeyword ?? string.Empty,
            Category = item.Category ?? string.Empty,
            Status = BatchQueueItemStatus.Pending.ToString()
        }).ToList();

        _context.BatchQueueItems.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task<BatchQueueItem?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.BatchQueueItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<BatchQueueItem>> GetPendingAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var pendingStatuses = new[] { BatchQueueItemStatus.Pending.ToString(), BatchQueueItemStatus.Processing.ToString() };
        var entities = await _context.BatchQueueItems
            .AsNoTracking()
            .Where(x => pendingStatuses.Contains(x.Status))
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<BatchQueueItem>> GetAllAsync(
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.BatchQueueItems
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task<BatchQueueItem?> UpdateItemAsync(
        BatchQueueItem item,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.BatchQueueItems
            .FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken);

        if (entity is null) return null;

        entity.Status = item.Status.ToString();
        entity.OverallScore = item.OverallScore;
        entity.OptimizedTitle = item.OptimizedTitle ?? string.Empty;
        entity.OptimizedDescription = item.OptimizedDescription ?? string.Empty;
        entity.OptimizedTags = JsonSerializer.Serialize(item.OptimizedTags ?? []);
        entity.OptimizedMaterials = JsonSerializer.Serialize(item.OptimizedMaterials ?? []);
        entity.RiskWarnings = JsonSerializer.Serialize(item.RiskWarnings ?? []);
        entity.Issues = JsonSerializer.Serialize(item.Issues ?? []);
        entity.ProcessedAt = item.ProcessedAt?.UtcDateTime;
        entity.ErrorMessage = item.ErrorMessage;
        entity.IsSyncedToEtsy = item.IsSyncedToEtsy;
        entity.SyncedAt = item.SyncedAt?.UtcDateTime;
        entity.AbTestExperimentId = item.AbTestExperimentId;
        entity.AbTestStatus = item.AbTestStatus;

        await _context.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task UpdateAbTestStatusAsync(
        long itemId,
        long experimentId,
        string abTestStatus,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.BatchQueueItems
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);

        if (entity != null)
        {
            entity.AbTestExperimentId = experimentId;
            entity.AbTestStatus = abTestStatus;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        var completedStatuses = new[]
        {
            BatchQueueItemStatus.Completed.ToString(),
            BatchQueueItemStatus.SyncedToEtsy.ToString(),
            BatchQueueItemStatus.RolledBack.ToString()
        };

        var items = await _context.BatchQueueItems
            .Where(x => completedStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        _context.BatchQueueItems.RemoveRange(items);
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.BatchQueueItems.ToListAsync(cancellationToken);
        _context.BatchQueueItems.RemoveRange(items);
        return await _context.SaveChangesAsync(cancellationToken);
    }

    private static BatchQueueItem ToModel(BatchQueueItemEntity e)
    {
        var status = Enum.TryParse<BatchQueueItemStatus>(e.Status, true, out var s) ? s : BatchQueueItemStatus.Pending;

        return new BatchQueueItem(
            e.Id,
            e.CreatedAt.ToUtcOffset(),
            e.ListingId,
            e.OriginalTitle,
            e.OriginalDescription,
            DeserializeList(e.OriginalTags),
            e.TargetKeyword,
            e.Category,
            status,
            e.OverallScore,
            e.OptimizedTitle,
            e.OptimizedDescription,
            DeserializeList(e.OptimizedTags),
            DeserializeList(e.OptimizedMaterials),
            DeserializeList(e.RiskWarnings),
            DeserializeList(e.Issues),
            e.ProcessedAt.HasValue ? e.ProcessedAt.Value.ToUtcOffset() : null,
            e.ErrorMessage,
            e.IsSyncedToEtsy,
            e.SyncedAt.HasValue ? e.SyncedAt.Value.ToUtcOffset() : null,
            e.AbTestExperimentId,
            e.AbTestStatus);
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
