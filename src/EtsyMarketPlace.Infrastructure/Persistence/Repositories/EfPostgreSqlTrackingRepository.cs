namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Domain.Tracking;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

public sealed class EfPostgreSqlTrackingRepository : ITrackingRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlTrackingRepository> _logger;

    public EfPostgreSqlTrackingRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlTrackingRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<TrackingItem> SaveCaptureAsync(TrackingCapture capture, CancellationToken cancellationToken = default)
    {
        if (capture == null) throw new ArgumentNullException(nameof(capture));

        var item = await _context.TrackingItems
            .FirstOrDefaultAsync(x => x.EntityType == (int)capture.EntityType && x.ExternalKey == capture.ExternalKey, cancellationToken);

        if (item is null)
        {
            item = new TrackingItemEntity
            {
                EntityType = (int)capture.EntityType,
                ExternalKey = capture.ExternalKey ?? string.Empty,
                DisplayName = capture.DisplayName ?? string.Empty,
                Url = capture.Url ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            _context.TrackingItems.Add(item);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var snap = capture.Snapshot;
        if (snap != null)
        {
            var snapshotEntity = new TrackingSnapshotEntity
            {
                TrackingItemId = item.Id,
                CapturedAt = snap.CapturedAt.UtcDateTime,
                Price = snap.Price,
                CurrencyCode = snap.CurrencyCode ?? string.Empty,
                Favorites = snap.Favorites,
                Views = snap.Views,
                ShopSales = snap.ShopSales,
                ReviewCount = snap.ReviewCount,
                ReviewAverage = snap.ReviewAverage,
                SeoScore = snap.SeoScore,
                MarketScore = snap.MarketScore,
                DemandScore = snap.DemandScore,
                CompetitionScore = snap.CompetitionScore,
                OpportunityScore = snap.OpportunityScore
            };

            _context.TrackingSnapshots.Add(snapshotEntity);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new TrackingItem(
            item.Id,
            (TrackingEntityType)item.EntityType,
            item.ExternalKey,
            item.DisplayName,
            item.Url,
            new DateTimeOffset(item.CreatedAt, TimeSpan.Zero));
    }

    public async Task<IReadOnlyList<TrackingItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.TrackingItems
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => new TrackingItem(
            e.Id,
            (TrackingEntityType)e.EntityType,
            e.ExternalKey,
            e.DisplayName,
            e.Url,
            new DateTimeOffset(e.CreatedAt, TimeSpan.Zero))).ToList();
    }

    public async Task<IReadOnlyList<TrackingSnapshot>> GetSnapshotsAsync(long trackingItemId, CancellationToken cancellationToken = default)
    {
        var snapshots = await _context.TrackingSnapshots
            .AsNoTracking()
            .Where(x => x.TrackingItemId == trackingItemId)
            .OrderByDescending(x => x.CapturedAt)
            .ToListAsync(cancellationToken);

        return snapshots.Select(s => new TrackingSnapshot(
            s.Id,
            s.TrackingItemId,
            new DateTimeOffset(s.CapturedAt, TimeSpan.Zero),
            s.Price,
            s.CurrencyCode,
            s.Favorites,
            s.Views,
            s.ShopSales,
            s.ReviewCount,
            s.ReviewAverage,
            s.SeoScore,
            s.MarketScore,
            s.DemandScore,
            s.CompetitionScore,
            s.OpportunityScore)).ToList();
    }

    public async Task DeleteItemAsync(long trackingItemId, CancellationToken cancellationToken = default)
    {
        var item = await _context.TrackingItems
            .FirstOrDefaultAsync(x => x.Id == trackingItemId, cancellationToken);

        if (item != null)
        {
            _context.TrackingItems.Remove(item);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
