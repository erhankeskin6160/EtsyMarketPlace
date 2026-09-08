namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.BatchQueue;
using Xunit;

public sealed class BulkAbTestLauncherTests
{
    private readonly InMemoryAbTestRepository _abRepo = new();
    private readonly InMemoryBatchQueueRepository _queueRepo = new();
    private readonly AbTestService _service;

    public BulkAbTestLauncherTests()
    {
        _service = new AbTestService(_abRepo, _queueRepo);
    }

    [Fact]
    public async Task BulkStartExperimentsAsync_CreatesExperimentsWith14DaysDurationAndCorrectVariants()
    {
        var requests = new List<BulkAbTestItemRequest>
        {
            new(
                BatchQueueItemId: 1,
                ListingId: "1001",
                OriginalTitle: "Vintage Wooden Box",
                OriginalDescription: "Old wooden storage",
                OriginalTags: ["box", "vintage"],
                OptimizedTitle: "Rustic Wooden Jewelry Box Handcrafted Keepsake",
                OptimizedDescription: "Modern artisan keepsake storage box",
                OptimizedTags: ["jewelry box", "wooden keepsake", "rustic decor"],
                CurrentViews: 120,
                CurrentFavorites: 15,
                CurrentSales: 3),
            new(
                BatchQueueItemId: 2,
                ListingId: "1002",
                OriginalTitle: "Leather Keyring",
                OriginalDescription: "Simple leather keychain",
                OriginalTags: ["keychain", "gift"],
                OptimizedTitle: "Personalized Leather Keychain Custom Monogram Gift",
                OptimizedDescription: "Full grain genuine leather key holder",
                OptimizedTags: ["monogram gift", "leather keychain", "anniversary gift"],
                CurrentViews: 45,
                CurrentFavorites: 4,
                CurrentSales: 1),
        };

        var options = new BulkAbTestLaunchOptions(
            DurationDays: 14,
            ExperimentPrefix: "[Parti 1]",
            AutoDeployVariantBToEtsy: false);

        var result = await _service.BulkStartExperimentsAsync(requests, options);

        Assert.Equal(2, result.TotalRequested);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, result.CreatedExperiments.Count);

        var exp1 = result.CreatedExperiments[0];
        Assert.Equal("1001", exp1.ListingId);
        Assert.Contains("14 Gün", exp1.ExperimentName);
        Assert.Equal("Vintage Wooden Box", exp1.VariantA_Title);
        Assert.Equal("Rustic Wooden Jewelry Box Handcrafted Keepsake", exp1.VariantB_Title);
        Assert.Equal(AbTestStatus.Active, exp1.Status);
        Assert.Equal(120, exp1.BeforeViews);
        Assert.Equal(15, exp1.BeforeFavorites);
        Assert.Equal(3, exp1.BeforeSales);
        Assert.NotNull(exp1.EndDate);
        Assert.True(exp1.EndDate.Value > exp1.StartDate.AddDays(13));
    }

    [Fact]
    public async Task BulkStartExperimentsAsync_WithAutoDeploy_InvokesEtsyDeployAction()
    {
        var requests = new List<BulkAbTestItemRequest>
        {
            new(
                BatchQueueItemId: 1,
                ListingId: "1003",
                OriginalTitle: "Ceramic Mug",
                OriginalDescription: "Coffee cup",
                OriginalTags: ["mug"],
                OptimizedTitle: "Handmade Ceramic Coffee Mug Pottery Cup",
                OptimizedDescription: "Artisan stoneware mug",
                OptimizedTags: ["stoneware", "coffee mug"])
        };

        var deployedListings = new List<(long id, string title)>();
        Func<long, string, string, IReadOnlyList<string>, Task> deployAction = (id, title, desc, tags) =>
        {
            deployedListings.Add((id, title));
            return Task.CompletedTask;
        };

        var options = new BulkAbTestLaunchOptions(
            DurationDays: 7,
            AutoDeployVariantBToEtsy: true);

        var result = await _service.BulkStartExperimentsAsync(requests, options, deployAction);

        Assert.Equal(1, result.SuccessCount);
        Assert.Single(deployedListings);
        Assert.Equal(1003, deployedListings[0].id);
        Assert.Equal("Handmade Ceramic Coffee Mug Pottery Cup", deployedListings[0].title);
    }

    [Fact]
    public void CheckSafetyGuardrail_WhenViewsDropOver40Percent_GeneratesSafetyAlert()
    {
        var experiment = new ListingAbTestExperiment(
            Id: 5,
            CreatedAt: DateTimeOffset.Now,
            ListingId: "1005",
            ListingTitle: "Dragon Figurine",
            ExperimentName: "A/B Dragon",
            VariantA_Title: "Dragon Figure",
            VariantB_Title: "Fantasy Mythical Dragon Statue",
            VariantA_Tags: ["dragon"],
            VariantB_Tags: ["statue"],
            VariantA_Description: "A",
            VariantB_Description: "B",
            StartDate: DateTimeOffset.Now,
            EndDate: DateTimeOffset.Now.AddDays(14),
            Status: AbTestStatus.Active,
            BeforeViews: 100,
            BeforeFavorites: 20,
            BeforeSales: 5,
            AfterViews: 45, // dropped by 55%
            AfterFavorites: 10,
            AfterSales: 2);

        var alert = _service.CheckSafetyGuardrail(experiment, dropThresholdPercent: 40.0);

        Assert.NotNull(alert);
        Assert.Equal(5, alert.ExperimentId);
        Assert.Equal("1005", alert.ListingId);
        Assert.True(alert.DropPercentage >= 50.0);
        Assert.True(alert.RecommendationRollback);
    }

    [Fact]
    public void CheckSafetyGuardrail_WhenViewsAreStableOrHigher_ReturnsNull()
    {
        var experiment = new ListingAbTestExperiment(
            Id: 6,
            CreatedAt: DateTimeOffset.Now,
            ListingId: "1006",
            ListingTitle: "Cat Bowl",
            ExperimentName: "A/B Cat Bowl",
            VariantA_Title: "Cat Dish",
            VariantB_Title: "Ceramic Cat Feeding Bowl",
            VariantA_Tags: ["cat"],
            VariantB_Tags: ["bowl"],
            VariantA_Description: "A",
            VariantB_Description: "B",
            StartDate: DateTimeOffset.Now,
            EndDate: DateTimeOffset.Now.AddDays(14),
            Status: AbTestStatus.Active,
            BeforeViews: 80,
            BeforeFavorites: 10,
            BeforeSales: 2,
            AfterViews: 110, // increased
            AfterFavorites: 15,
            AfterSales: 4);

        var alert = _service.CheckSafetyGuardrail(experiment, dropThresholdPercent: 40.0);

        Assert.Null(alert);
    }

    [Fact]
    public async Task ResolveExperimentAsync_WithVariantA_RollsBackToOriginalAndCompletes()
    {
        var save = new SaveAbTestExperiment(
            "1007",
            "Listing Title",
            "Experiment 7",
            "Original Title",
            "AI Title",
            ["orig1"],
            ["ai1"],
            "Orig Desc",
            "AI Desc",
            InitialViews: 50);

        var exp = await _service.StartExperimentAsync(save);

        string? appliedTitle = null;
        Func<long, string, string, IReadOnlyList<string>, Task> deployAction = (id, title, desc, tags) =>
        {
            appliedTitle = title;
            return Task.CompletedTask;
        };

        var resolved = await _service.ResolveExperimentAsync(exp.Id, "VariantA", deployAction);

        Assert.Equal(AbTestStatus.Completed, resolved.Status);
        Assert.Equal("Original Title", appliedTitle);
    }

    [Fact]
    public async Task ResolveExperimentAsync_WithVariantB_AppliesAiVariantAndCompletes()
    {
        var save = new SaveAbTestExperiment(
            "1008",
            "Listing Title",
            "Experiment 8",
            "Original Title",
            "AI Title",
            ["orig1"],
            ["ai1"],
            "Orig Desc",
            "AI Desc",
            InitialViews: 50);

        var exp = await _service.StartExperimentAsync(save);

        string? appliedTitle = null;
        Func<long, string, string, IReadOnlyList<string>, Task> deployAction = (id, title, desc, tags) =>
        {
            appliedTitle = title;
            return Task.CompletedTask;
        };

        var resolved = await _service.ResolveExperimentAsync(exp.Id, "VariantB", deployAction);

        Assert.Equal(AbTestStatus.Completed, resolved.Status);
        Assert.Equal("AI Title", appliedTitle);
    }

    private sealed class InMemoryAbTestRepository : IAbTestRepository
    {
        private readonly List<ListingAbTestExperiment> _items = [];
        private long _nextId = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ListingAbTestExperiment> SaveAsync(SaveAbTestExperiment experiment, CancellationToken cancellationToken = default)
        {
            var item = new ListingAbTestExperiment(
                _nextId++,
                DateTimeOffset.Now,
                experiment.ListingId,
                experiment.ListingTitle,
                experiment.ExperimentName,
                experiment.VariantA_Title,
                experiment.VariantB_Title,
                experiment.VariantA_Tags,
                experiment.VariantB_Tags,
                experiment.VariantA_Description,
                experiment.VariantB_Description,
                DateTimeOffset.Now,
                experiment.EndDate,
                AbTestStatus.Active,
                experiment.InitialViews,
                experiment.InitialFavorites,
                experiment.InitialSales,
                experiment.InitialViews,
                experiment.InitialFavorites,
                experiment.InitialSales);
            _items.Add(item);
            return Task.FromResult(item);
        }

        public Task<ListingAbTestExperiment?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<ListingAbTestExperiment>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ListingAbTestExperiment>>(_items.OrderByDescending(i => i.Id).Take(limit).ToList());

        public Task<IReadOnlyList<ListingAbTestExperiment>> GetByListingIdAsync(string listingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ListingAbTestExperiment>>(_items.Where(i => i.ListingId == listingId).ToList());

        public Task<ListingAbTestExperiment?> UpdateMetricsAsync(UpdateAbTestMetrics update, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(i => i.Id == update.ExperimentId);
            if (index < 0) return Task.FromResult<ListingAbTestExperiment?>(null);

            var existing = _items[index];
            var updated = existing with
            {
                AfterViews = update.AfterViews,
                AfterFavorites = update.AfterFavorites,
                AfterSales = update.AfterSales,
                Status = update.CompleteExperiment ? AbTestStatus.Completed : existing.Status,
                EndDate = update.CompleteExperiment ? DateTimeOffset.Now : existing.EndDate,
            };
            _items[index] = updated;
            return Task.FromResult<ListingAbTestExperiment?>(updated);
        }

        public Task<ListingAbTestExperiment?> UpdateStatusAsync(long id, AbTestStatus status, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(i => i.Id == id);
            if (index < 0) return Task.FromResult<ListingAbTestExperiment?>(null);

            var existing = _items[index];
            var updated = existing with
            {
                Status = status,
                EndDate = status == AbTestStatus.Completed ? DateTimeOffset.Now : existing.EndDate,
            };
            _items[index] = updated;
            return Task.FromResult<ListingAbTestExperiment?>(updated);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var removed = _items.RemoveAll(i => i.Id == id);
            return Task.FromResult(removed > 0);
        }
    }

    private sealed class InMemoryBatchQueueRepository : IBatchQueueRepository
    {
        private readonly List<BatchQueueItem> _items = [];
        private long _nextId = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BatchQueueItem>> EnqueueBatchAsync(IReadOnlyList<SaveBatchQueueItem> items, CancellationToken cancellationToken = default)
        {
            var result = items.Select(i => new BatchQueueItem(
                _nextId++,
                DateTimeOffset.Now,
                i.ListingId,
                i.OriginalTitle,
                i.OriginalDescription,
                i.OriginalTags,
                i.TargetKeyword,
                i.Category,
                BatchQueueItemStatus.Pending,
                0,
                "",
                "",
                [],
                [],
                [],
                [],
                null,
                null)).ToList();
            _items.AddRange(result);
            return Task.FromResult<IReadOnlyList<BatchQueueItem>>(result);
        }

        public Task<BatchQueueItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<BatchQueueItem>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BatchQueueItem>>(_items.Where(i => i.Status == BatchQueueItemStatus.Pending).Take(limit).ToList());

        public Task<IReadOnlyList<BatchQueueItem>> GetAllAsync(int limit = 500, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BatchQueueItem>>(_items.Take(limit).ToList());

        public Task<BatchQueueItem?> UpdateItemAsync(BatchQueueItem item, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(i => i.Id == item.Id);
            if (index < 0) return Task.FromResult<BatchQueueItem?>(null);
            _items[index] = item;
            return Task.FromResult<BatchQueueItem?>(item);
        }

        public Task UpdateAbTestStatusAsync(long itemId, long experimentId, string abTestStatus, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(i => i.Id == itemId);
            if (index >= 0)
            {
                _items[index] = _items[index] with
                {
                    AbTestExperimentId = experimentId,
                    AbTestStatus = abTestStatus
                };
            }
            return Task.CompletedTask;
        }

        public Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
        {
            var count = _items.RemoveAll(i => i.Status == BatchQueueItemStatus.Completed);
            return Task.FromResult(count);
        }

        public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
        {
            var count = _items.Count;
            _items.Clear();
            return Task.FromResult(count);
        }
    }
}
