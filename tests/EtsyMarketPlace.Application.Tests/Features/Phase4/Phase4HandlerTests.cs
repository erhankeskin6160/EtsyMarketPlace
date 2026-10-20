namespace EtsyMarketPlace.Application.Tests.Features.Phase4;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.Features.AbTests.Commands.CreateAbTest;
using EtsyMarketPlace.Application.Features.Auth.Commands.Login;
using EtsyMarketPlace.Application.Features.Auth.Commands.Register;
using EtsyMarketPlace.Application.Features.BatchQueue.Commands.ProcessBatchItem;
using EtsyMarketPlace.Application.Features.Tracking.Queries.GetTrackingItems;
using EtsyMarketPlace.Application.Tracking;
using EtsyMarketPlace.Domain.Tracking;
using Xunit;

public sealed class Phase4HandlerTests
{
    [Fact]
    public void CreateAbTestValidator_RejectsEmptyFields()
    {
        var validator = new CreateAbTestValidator();
        var request = new CreateAbTestRequest
        {
            Experiment = new SaveAbTestExperiment(
                ListingId: "",
                ListingTitle: "Test",
                ExperimentName: "",
                VariantA_Title: "A",
                VariantB_Title: "B",
                VariantA_Tags: new List<string>(),
                VariantB_Tags: new List<string>(),
                VariantA_Description: "Desc A",
                VariantB_Description: "Desc B")
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Experiment.ListingId");
        Assert.Contains(result.Errors, e => e.PropertyName == "Experiment.ExperimentName");
    }

    [Fact]
    public async Task CreateAbTestHandler_CreatesAndReturnsExperiment()
    {
        var fakeRepo = new FakeAbTestRepository();
        var handler = new CreateAbTestHandler(fakeRepo);

        var request = new CreateAbTestRequest
        {
            Experiment = new SaveAbTestExperiment(
                ListingId: "123456",
                ListingTitle: "Sample Listing",
                ExperimentName: "SEO Title Test",
                VariantA_Title: "Variant A",
                VariantB_Title: "Variant B",
                VariantA_Tags: new List<string> { "tag1" },
                VariantB_Tags: new List<string> { "tag2" },
                VariantA_Description: "Desc A",
                VariantB_Description: "Desc B")
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        var exp = Assert.IsType<ListingAbTestExperiment>(response.Data);
        Assert.Equal("SEO Title Test", exp.ExperimentName);
        Assert.Single(fakeRepo.SavedExperiments);
    }

    [Fact]
    public void LoginUserValidator_RejectsEmptyCredentials()
    {
        var validator = new LoginUserValidator();
        var request = new LoginUserRequest
        {
            UsernameOrEmail = "",
            Password = ""
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "UsernameOrEmail");
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void RegisterUserValidator_RejectsInvalidInput()
    {
        var validator = new RegisterUserValidator();
        var request = new RegisterUserRequest
        {
            Username = "ab", // < 3 chars
            Email = "invalid-email",
            Password = "123" // < 6 chars
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Username");
        Assert.Contains(result.Errors, e => e.PropertyName == "Email");
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task GetTrackingItemsHandler_ReturnsListFromRepo()
    {
        var fakeRepo = new FakeTrackingRepository();
        fakeRepo.Items.Add(new TrackingItem(
            Id: 1,
            EntityType: TrackingEntityType.Listing,
            ExternalKey: "12345",
            DisplayName: "Test Item",
            Url: "https://etsy.com/listing/12345",
            CreatedAt: DateTimeOffset.UtcNow));

        var handler = new GetTrackingItemsHandler(fakeRepo);
        var response = await handler.Handle(new GetTrackingItemsRequest(), CancellationToken.None);

        Assert.True(response.Success);
        var items = Assert.IsAssignableFrom<IReadOnlyList<TrackingItem>>(response.Data);
        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
    }

    [Fact]
    public async Task ProcessBatchItemHandler_UpdatesBatchItemStatus()
    {
        var fakeRepo = new FakeBatchQueueRepository();
        fakeRepo.Items.Add(new BatchQueueItem(
            Id: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            ListingId: "12345",
            OriginalTitle: "Old",
            OriginalDescription: "Old Desc",
            OriginalTags: new List<string>(),
            TargetKeyword: "sample",
            Category: "crafts",
            Status: BatchQueueItemStatus.Pending,
            OverallScore: 0,
            OptimizedTitle: "",
            OptimizedDescription: "",
            OptimizedTags: new List<string>(),
            OptimizedMaterials: new List<string>(),
            RiskWarnings: new List<string>(),
            Issues: new List<string>(),
            ProcessedAt: null,
            ErrorMessage: null));

        var handler = new ProcessBatchItemHandler(fakeRepo);
        var request = new ProcessBatchItemRequest
        {
            Id = 1,
            Status = BatchQueueItemStatus.Completed,
            OptimizedTitle = "New Title",
            OverallScore = 95
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(BatchQueueItemStatus.Completed, fakeRepo.Items[0].Status);
        Assert.Equal("New Title", fakeRepo.Items[0].OptimizedTitle);
    }

    private sealed class FakeAbTestRepository : IAbTestRepository
    {
        public List<ListingAbTestExperiment> SavedExperiments { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ListingAbTestExperiment> SaveAsync(SaveAbTestExperiment experiment, CancellationToken cancellationToken = default)
        {
            var entity = new ListingAbTestExperiment(
                Id: SavedExperiments.Count + 1,
                CreatedAt: DateTimeOffset.UtcNow,
                ListingId: experiment.ListingId,
                ListingTitle: experiment.ListingTitle,
                ExperimentName: experiment.ExperimentName,
                VariantA_Title: experiment.VariantA_Title,
                VariantB_Title: experiment.VariantB_Title,
                VariantA_Tags: experiment.VariantA_Tags,
                VariantB_Tags: experiment.VariantB_Tags,
                VariantA_Description: experiment.VariantA_Description,
                VariantB_Description: experiment.VariantB_Description,
                StartDate: DateTimeOffset.UtcNow,
                EndDate: experiment.EndDate,
                Status: AbTestStatus.Active,
                BeforeViews: experiment.InitialViews,
                BeforeFavorites: experiment.InitialFavorites,
                BeforeSales: experiment.InitialSales,
                AfterViews: 0,
                AfterFavorites: 0,
                AfterSales: 0);

            SavedExperiments.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<ListingAbTestExperiment?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            var exp = SavedExperiments.Find(e => e.Id == id);
            return Task.FromResult(exp);
        }

        public Task<IReadOnlyList<ListingAbTestExperiment>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingAbTestExperiment>>(SavedExperiments);

        public Task<IReadOnlyList<ListingAbTestExperiment>> GetByListingIdAsync(string listingId, CancellationToken cancellationToken = default)
        {
            var results = SavedExperiments.FindAll(e => e.ListingId == listingId);
            return Task.FromResult<IReadOnlyList<ListingAbTestExperiment>>(results);
        }

        public Task<ListingAbTestExperiment?> UpdateMetricsAsync(UpdateAbTestMetrics update, CancellationToken cancellationToken = default)
            => Task.FromResult<ListingAbTestExperiment?>(null);

        public Task<ListingAbTestExperiment?> UpdateStatusAsync(long id, AbTestStatus status, CancellationToken cancellationToken = default)
        {
            var exp = SavedExperiments.Find(e => e.Id == id);
            return Task.FromResult(exp);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var count = SavedExperiments.RemoveAll(e => e.Id == id);
            return Task.FromResult(count > 0);
        }
    }

    private sealed class FakeTrackingRepository : ITrackingRepository
    {
        public List<TrackingItem> Items { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TrackingItem> SaveCaptureAsync(TrackingCapture capture, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<TrackingItem>> GetItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TrackingItem>>(Items);

        public Task<IReadOnlyList<TrackingSnapshot>> GetSnapshotsAsync(long trackingItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TrackingSnapshot>>([]);

        public Task DeleteItemAsync(long trackingItemId, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(i => i.Id == trackingItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBatchQueueRepository : IBatchQueueRepository
    {
        public List<BatchQueueItem> Items { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BatchQueueItem>> EnqueueBatchAsync(IReadOnlyList<SaveBatchQueueItem> items, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BatchQueueItem>>(Items);

        public Task<IReadOnlyList<BatchQueueItem>> GetAllAsync(int limit = 500, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BatchQueueItem>>(Items);

        public Task<IReadOnlyList<BatchQueueItem>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BatchQueueItem>>(Items.FindAll(i => i.Status == BatchQueueItemStatus.Pending));

        public Task<BatchQueueItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Find(i => i.Id == id));

        public Task<BatchQueueItem?> UpdateItemAsync(BatchQueueItem item, CancellationToken cancellationToken = default)
        {
            var idx = Items.FindIndex(i => i.Id == item.Id);
            if (idx >= 0)
            {
                Items[idx] = item;
                return Task.FromResult<BatchQueueItem?>(item);
            }
            return Task.FromResult<BatchQueueItem?>(null);
        }

        public Task UpdateAbTestStatusAsync(long itemId, long experimentId, string abTestStatus, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
        {
            var count = Items.RemoveAll(i => i.Status == BatchQueueItemStatus.Completed);
            return Task.FromResult(count);
        }

        public Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
        {
            var count = Items.Count;
            Items.Clear();
            return Task.FromResult(count);
        }
    }
}
