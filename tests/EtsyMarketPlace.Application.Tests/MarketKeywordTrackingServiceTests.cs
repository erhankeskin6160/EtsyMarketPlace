namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using Xunit;

public sealed class MarketKeywordTrackingServiceTests
{
    private sealed class FakeTrackingRepository : ITrackingRepository
    {
        public List<TrackingItem> Items { get; } = new();
        public Dictionary<long, List<TrackingSnapshot>> Snapshots { get; } = new();
        public TrackingCapture? LastCapture { get; private set; }

        private long _nextItemId = 1;
        private long _nextSnapshotId = 1;

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<TrackingItem> SaveCaptureAsync(TrackingCapture capture, CancellationToken ct = default)
        {
            LastCapture = capture;
            var existing = Items.FirstOrDefault(i =>
                i.EntityType == capture.EntityType &&
                i.ExternalKey == capture.ExternalKey.Trim());

            if (existing == null)
            {
                existing = new TrackingItem(
                    _nextItemId++,
                    capture.EntityType,
                    capture.ExternalKey.Trim(),
                    capture.DisplayName,
                    capture.Url,
                    DateTimeOffset.UtcNow);
                Items.Add(existing);
            }

            if (!Snapshots.TryGetValue(existing.Id, out var list))
            {
                list = new List<TrackingSnapshot>();
                Snapshots[existing.Id] = list;
            }

            list.Add(capture.Snapshot with { Id = _nextSnapshotId++, TrackingItemId = existing.Id });
            return Task.FromResult(existing);
        }

        public Task<IReadOnlyList<TrackingItem>> GetItemsAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<TrackingItem>)Items.ToList());

        public Task<IReadOnlyList<TrackingSnapshot>> GetSnapshotsAsync(long trackingItemId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<TrackingSnapshot>)(Snapshots.TryGetValue(trackingItemId, out var list)
                ? list.ToList()
                : new List<TrackingSnapshot>()));

        public Task DeleteItemAsync(long trackingItemId, CancellationToken ct = default)
        {
            Items.RemoveAll(i => i.Id == trackingItemId);
            Snapshots.Remove(trackingItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMarketResearchService : IMarketResearchService
    {
        public MarketSearchResponse Response { get; set; } = new();

        public Task<MarketSearchResponse> SearchMarketAsync(MarketSearchQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Response);
    }

    private static MarketSearchResponse BuildResponse(int total, decimal avgPrice, double avgFavorites, int opportunity, int sampleSize)
        => new()
        {
            Keyword = "k",
            Total = total,
            Kpis = new MarketSummaryKpisDto
            {
                TotalListings = sampleSize,
                AveragePrice = avgPrice,
                AverageFavorites = avgFavorites,
                OpportunityScore = opportunity
            },
            Listings = new()
        };

    [Fact]
    public async Task TrackKeywordAsync_BuildsSnapshotFromSearchKpis()
    {
        var repo = new FakeTrackingRepository();
        var market = new FakeMarketResearchService
        {
            Response = BuildResponse(total: 14314, avgPrice: 42.56m, avgFavorites: 120.4, opportunity: 77, sampleSize: 25)
        };
        var service = new MarketKeywordTrackingService(market, repo);

        var history = await service.TrackKeywordAsync("  Dragon Lamp  ");

        Assert.NotNull(repo.LastCapture);
        Assert.Equal(TrackingEntityType.Keyword, repo.LastCapture!.EntityType);
        Assert.Equal("dragon lamp", repo.LastCapture.ExternalKey);
        Assert.Equal("Dragon Lamp", repo.LastCapture.DisplayName);
        Assert.Contains("etsy.com/search", repo.LastCapture.Url);

        var snap = repo.LastCapture.Snapshot;
        Assert.Equal(42.56m, snap.Price);
        Assert.Equal("USD", snap.CurrencyCode);
        Assert.Equal(120, snap.Favorites);
        Assert.Equal(77, snap.OpportunityScore);
        Assert.Equal(14314, snap.ResultCount);
        Assert.Equal(25, snap.SampleSize);

        Assert.Single(history.Snapshots);
        Assert.Equal(history.Item.Id, history.Latest!.TrackingItemId);
    }

    [Fact]
    public async Task TrackKeywordAsync_Twice_AppendsSnapshotUnderSameItem()
    {
        var repo = new FakeTrackingRepository();
        var market = new FakeMarketResearchService { Response = BuildResponse(100, 10m, 5, 50, 25) };
        var service = new MarketKeywordTrackingService(market, repo);

        await service.TrackKeywordAsync("dragon lamp");
        var second = await service.TrackKeywordAsync("Dragon Lamp");

        Assert.Single(repo.Items);
        Assert.Equal(2, second.Snapshots.Count);
        Assert.NotNull(second.Previous);
    }

    [Fact]
    public async Task GetTrackedKeywords_ReturnsOnlyKeywords_OrderedByLatest()
    {
        var repo = new FakeTrackingRepository();
        var market = new FakeMarketResearchService { Response = BuildResponse(10, 1m, 1, 10, 1) };
        var service = new MarketKeywordTrackingService(market, repo);

        // İlgisiz bir listing takibi (keyword listesine girmemeli)
        await repo.SaveCaptureAsync(new TrackingCapture(
            TrackingEntityType.Listing, "111", "Some Listing", "https://x", new TrackingSnapshot(0, 0, DateTimeOffset.UtcNow)));

        await service.TrackKeywordAsync("alpha");
        await Task.Delay(20);
        await service.TrackKeywordAsync("beta");

        var list = await service.GetTrackedKeywordsAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("beta", list[0].Item.DisplayName);
        Assert.Equal("alpha", list[1].Item.DisplayName);
    }

    [Fact]
    public async Task DeleteKeywordAsync_RemovesItemAndSnapshots()
    {
        var repo = new FakeTrackingRepository();
        var market = new FakeMarketResearchService { Response = BuildResponse(10, 1m, 1, 10, 1) };
        var service = new MarketKeywordTrackingService(market, repo);

        await service.TrackKeywordAsync("silinecek kelime");

        Assert.True(await service.DeleteKeywordAsync("Silinecek Kelime"));
        Assert.Empty(repo.Items);
        Assert.False(await service.DeleteKeywordAsync("silinecek kelime"));
    }
}
