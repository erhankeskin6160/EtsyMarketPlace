namespace EtsyMarketPlace.Application.MarketResearch;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;

public interface IMarketKeywordTrackingService
{
    Task<TrackingHistory> TrackKeywordAsync(string keyword, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackingHistory>> GetTrackedKeywordsAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteKeywordAsync(string keyword, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pazar araştırması kelimelerini mevcut takip altyapısı (SQLite) üzerinden tarihli snapshot'larla izler.
/// Her "takibe al" çağrısı canlı pazar aramasının KPI'larından bir görüntü kaydeder; aynı kelime
/// tekrar takibe alındığında snapshot geçmişine eklenir ve değişim (delta) hesaplanabilir.
/// </summary>
public sealed class MarketKeywordTrackingService : IMarketKeywordTrackingService
{
    private const int SnapshotSampleLimit = 25;

    private readonly IMarketResearchService _marketResearchService;
    private readonly ITrackingRepository _trackingRepository;

    public MarketKeywordTrackingService(
        IMarketResearchService marketResearchService,
        ITrackingRepository trackingRepository)
    {
        _marketResearchService = marketResearchService ?? throw new ArgumentNullException(nameof(marketResearchService));
        _trackingRepository = trackingRepository ?? throw new ArgumentNullException(nameof(trackingRepository));
    }

    public async Task<TrackingHistory> TrackKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var display = (keyword ?? string.Empty).Trim();
        if (display.Length == 0)
        {
            throw new ArgumentException("Takip için kelime gerekli.", nameof(keyword));
        }

        var search = await _marketResearchService.SearchMarketAsync(
            new MarketSearchQuery(Keyword: display, Limit: SnapshotSampleLimit, SortBy: "market_score"),
            cancellationToken);

        var kpis = search.Kpis;
        var snapshot = new TrackingSnapshot(
            Id: 0,
            TrackingItemId: 0,
            CapturedAt: DateTimeOffset.UtcNow,
            Price: kpis.AveragePrice,
            CurrencyCode: "USD",
            Favorites: (int)Math.Round(kpis.AverageFavorites),
            OpportunityScore: kpis.OpportunityScore,
            ResultCount: search.Total,
            SampleSize: kpis.TotalListings);

        var capture = new TrackingCapture(
            EntityType: TrackingEntityType.Keyword,
            ExternalKey: display.ToLowerInvariant(),
            DisplayName: display,
            Url: $"https://www.etsy.com/search?q={Uri.EscapeDataString(display)}",
            Snapshot: snapshot);

        var item = await _trackingRepository.SaveCaptureAsync(capture, cancellationToken);
        var snapshots = await _trackingRepository.GetSnapshotsAsync(item.Id, cancellationToken);
        return new TrackingHistory(item, snapshots);
    }

    public async Task<IReadOnlyList<TrackingHistory>> GetTrackedKeywordsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _trackingRepository.GetItemsAsync(cancellationToken);
        var histories = new List<TrackingHistory>();
        foreach (var item in items.Where(i => i.EntityType == TrackingEntityType.Keyword))
        {
            var snapshots = await _trackingRepository.GetSnapshotsAsync(item.Id, cancellationToken);
            histories.Add(new TrackingHistory(item, snapshots));
        }

        return histories
            .OrderByDescending(h => h.Latest?.CapturedAt ?? h.Item.CreatedAt)
            .ToList();
    }

    public async Task<bool> DeleteKeywordAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var key = (keyword ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0)
        {
            return false;
        }

        var items = await _trackingRepository.GetItemsAsync(cancellationToken);
        var match = items.FirstOrDefault(i =>
            i.EntityType == TrackingEntityType.Keyword &&
            string.Equals(i.ExternalKey, key, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            return false;
        }

        await _trackingRepository.DeleteItemAsync(match.Id, cancellationToken);
        return true;
    }
}
