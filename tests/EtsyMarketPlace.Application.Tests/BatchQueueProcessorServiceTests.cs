namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class BatchQueueProcessorServiceTests
{
    private readonly InMemoryBatchQueueRepository _repository = new();
    private readonly StubAiOptimizer _aiOptimizer = new();
    private readonly BatchQueueProcessorService _service;

    public BatchQueueProcessorServiceTests()
    {
        _service = new BatchQueueProcessorService(_repository, _aiOptimizer);
    }

    [Fact]
    public async Task EnqueueAsync_SavesPendingItems()
    {
        var items = new List<SaveBatchQueueItem>
        {
            new("list-1", "Dragon Bust Resin", "Description 1", ["tag1", "tag2"], "dragon bust"),
            new("list-2", "Dragon Sculpture Art", "Description 2", ["tag3", "tag4"], "dragon sculpture"),
        };

        var enqueued = await _service.EnqueueAsync(items);

        Assert.Equal(2, enqueued.Count);
        Assert.All(enqueued, i => Assert.Equal(BatchQueueItemStatus.Pending, i.Status));
    }

    [Fact]
    public async Task ProcessQueueAsync_OptimizesAndValidatesItems()
    {
        var items = new List<SaveBatchQueueItem>
        {
            new("list-1", "Dragon Bust Resin Fantasy Shelf Decor Collectible", "Detailed description of resin dragon bust.", ["dragon bust", "resin figure"], "dragon bust"),
        };

        await _service.EnqueueAsync(items);

        var progressSummary = new List<BatchQueueSummary>();
        var progress = new Progress<BatchQueueSummary>(s => progressSummary.Add(s));

        await _service.ProcessQueueAsync(progress);

        var all = await _service.GetAllAsync();
        var processed = all.First();

        Assert.True(processed.Status is BatchQueueItemStatus.Completed or BatchQueueItemStatus.RiskWarning);
        Assert.True(processed.OverallScore > 0);
        Assert.NotEmpty(processed.OptimizedTitle);
        Assert.NotEmpty(processed.OptimizedTags);
    }

    [Fact]
    public async Task ProcessQueueAsync_DetectsRiskWarnings()
    {
        var riskOptimizer = new RiskStubAiOptimizer();
        var service = new BatchQueueProcessorService(_repository, riskOptimizer);

        var items = new List<SaveBatchQueueItem>
        {
            new("list-1", "Disney Frozen Elsa Dragon Bust Pokemon Marvel", "Disney Frozen Elsa Marvel Pokemon", ["disney", "pokemon"], "dragon bust"),
        };

        await service.EnqueueAsync(items);
        await service.ProcessQueueAsync();

        var all = await service.GetAllAsync();
        var processed = all.First();

        Assert.Equal(BatchQueueItemStatus.RiskWarning, processed.Status);
        Assert.NotEmpty(processed.RiskWarnings);
    }

    [Fact]
    public async Task ClearCompletedAsync_RemovesCompletedItems()
    {
        var items = new List<SaveBatchQueueItem>
        {
            new("list-1", "Dragon Bust", "Desc", ["tag1"], "dragon bust"),
        };

        await _service.EnqueueAsync(items);
        await _service.ProcessQueueAsync();

        await _service.ClearCompletedAsync();

        var remaining = await _service.GetAllAsync();
        Assert.Empty(remaining);
    }

    private sealed class StubAiOptimizer : IAiListingOptimizer
    {
        public Task<ListingOptimizationResult> OptimizeAsync(ListingOptimizationInput input, CancellationToken cancellationToken = default)
        {
            var titleSuggestions = new[]
            {
                "Hand Painted Resin Dragon Bust Fantasy Shelf Decor Collectors Gift",
                "Dragon Bust Sculpture Collectible Shelf Display Decor",
                "Resin Dragon Figure Hand Painted Fantasy Collector Piece",
            };
            var tagSuggestions = new[]
            {
                "dragon bust", "resin figure", "fantasy decor", "shelf decor", "collector gift",
                "dragon sculpture", "hand painted", "tabletop decor", "fantasy gift", "resin art",
                "dragon art", "gamer gift", "unique decor",
            };

            return Task.FromResult(new ListingOptimizationResult(
                70,
                90,
                titleSuggestions,
                tagSuggestions,
                ["resin", "acrylic paint"],
                "This beautifully crafted dragon bust is made from high-quality resin and hand painted with intricate detail.\n\nPerfect for fantasy lovers and collectors who want a unique shelf piece.\n\nMaterials: Premium casting resin with acrylic paint finish. Approximate dimensions: 6 x 4 x 5 inches.\n\nPublishing review: No known brand or IP conflicts detected.",
                [],
                [],
                []));
        }
    }

    private sealed class RiskStubAiOptimizer : IAiListingOptimizer
    {
        public Task<ListingOptimizationResult> OptimizeAsync(ListingOptimizationInput input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ListingOptimizationResult(
                50, 50,
                ["Disney Frozen Elsa Dragon Bust Marvel Spider-Man"],
                ["disney", "frozen", "elsa", "marvel", "spider-man"],
                ["resin"],
                "Disney Frozen Elsa themed dragon bust Marvel inspired.",
                [], ["Disney", "Marvel"], []));
        }
    }

    private sealed class InMemoryBatchQueueRepository : IBatchQueueRepository
    {
        private readonly List<BatchQueueItem> _items = [];
        private long _nextId = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BatchQueueItem>> EnqueueBatchAsync(IReadOnlyList<SaveBatchQueueItem> items, CancellationToken cancellationToken = default)
        {
            var result = new List<BatchQueueItem>();
            foreach (var item in items)
            {
                var qItem = new BatchQueueItem(
                    _nextId++,
                    DateTimeOffset.Now,
                    item.ListingId,
                    item.OriginalTitle,
                    item.OriginalDescription,
                    item.OriginalTags,
                    item.TargetKeyword,
                    item.Category,
                    BatchQueueItemStatus.Pending,
                    0, "", "", [], [], [], [], null, null);
                _items.Add(qItem);
                result.Add(qItem);
            }
            return Task.FromResult<IReadOnlyList<BatchQueueItem>>(result);
        }

        public Task<BatchQueueItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<BatchQueueItem>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BatchQueueItem>>(_items.Where(i => i.Status == BatchQueueItemStatus.Pending).Take(limit).ToList());

        public Task<IReadOnlyList<BatchQueueItem>> GetAllAsync(int limit = 500, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BatchQueueItem>>(_items.OrderByDescending(i => i.Id).Take(limit).ToList());

        public Task<BatchQueueItem?> UpdateItemAsync(BatchQueueItem item, CancellationToken cancellationToken = default)
        {
            var idx = _items.FindIndex(i => i.Id == item.Id);
            if (idx >= 0)
            {
                _items[idx] = item;
                return Task.FromResult<BatchQueueItem?>(item);
            }
            return Task.FromResult<BatchQueueItem?>(null);
        }

        public Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
        {
            var count = _items.RemoveAll(i => i.Status is BatchQueueItemStatus.Completed or BatchQueueItemStatus.RiskWarning or BatchQueueItemStatus.Failed);
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
