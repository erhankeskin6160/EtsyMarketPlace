namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Infrastructure.BatchQueue;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class BatchQueueEtsySyncTests
{
    private static (BatchQueueProcessorService Service, string DbPath) CreateTestService()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test-batch-queue-{Guid.NewGuid():N}.db");
        var repo = new SqliteBatchQueueRepository(tempDb);
        repo.InitializeAsync().GetAwaiter().GetResult();

        // Dummy optimizer for processor
        var optimizer = new MockOptimizer();
        var service = new BatchQueueProcessorService(repo, optimizer);
        return (service, tempDb);
    }

    private static void Cleanup(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }
        try { if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal"); } catch { }
        try { if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm"); } catch { }
    }

    [Fact]
    public async Task SyncItemToEtsy_UpdatesStatusAndCallsApi()
    {
        var (service, dbPath) = CreateTestService();
        try
        {
            var enqueued = await service.EnqueueAsync([
                new SaveBatchQueueItem("12345678", "Old Title", "Old Desc", ["tag1"], "prop")
            ]);
            var item = enqueued[0];

            // Manually set optimized data for testing
            await service.ProcessQueueAsync();

            long calledListingId = 0;
            string calledTitle = "";

            var synced = await service.SyncItemToEtsyAsync(
                item.Id,
                (lid, title, desc, tags, mats) =>
                {
                    calledListingId = lid;
                    calledTitle = title;
                    return Task.CompletedTask;
                });

            Assert.Equal(12345678L, calledListingId);
            Assert.NotEmpty(calledTitle);
            Assert.Equal(BatchQueueItemStatus.SyncedToEtsy, synced.Status);
            Assert.True(synced.IsSyncedToEtsy);
            Assert.NotNull(synced.SyncedAt);
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public async Task RollbackItem_RestoresOriginalDataAndMarksRolledBack()
    {
        var (service, dbPath) = CreateTestService();
        try
        {
            var enqueued = await service.EnqueueAsync([
                new SaveBatchQueueItem("98765432", "Original Watch Title", "Original Desc", ["original-tag"], "watch")
            ]);
            var item = enqueued[0];

            await service.ProcessQueueAsync();

            // First sync to Etsy
            await service.SyncItemToEtsyAsync(item.Id, (_, _, _, _, _) => Task.CompletedTask);

            long rollbackListingId = 0;
            string rollbackTitle = "";

            // Now rollback
            var rolledBack = await service.RollbackItemAsync(
                item.Id,
                (lid, title, desc, tags, mats) =>
                {
                    rollbackListingId = lid;
                    rollbackTitle = title;
                    return Task.CompletedTask;
                });

            Assert.Equal(98765432L, rollbackListingId);
            Assert.Equal("Original Watch Title", rollbackTitle);
            Assert.Equal(BatchQueueItemStatus.RolledBack, rolledBack.Status);
            Assert.False(rolledBack.IsSyncedToEtsy);
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    [Fact]
    public async Task SyncBatchToEtsy_AppliesThrottlingAndReportsProgress()
    {
        var (service, dbPath) = CreateTestService();
        try
        {
            var enqueued = await service.EnqueueAsync([
                new SaveBatchQueueItem("111111", "Product 1", "Desc 1", ["tag1"], "k1"),
                new SaveBatchQueueItem("222222", "Product 2", "Desc 2", ["tag2"], "k2")
            ]);

            await service.ProcessQueueAsync();

            var progressReports = new List<BatchSyncProgress>();
            var progress = new Progress<BatchSyncProgress>(p => progressReports.Add(p));

            var result = await service.SyncBatchToEtsyAsync(
                [enqueued[0].Id, enqueued[1].Id],
                (_, _, _, _, _) => Task.CompletedTask,
                progress);

            Assert.Equal(2, result.TotalRequested);
            Assert.Equal(2, result.SuccessCount);
            Assert.Equal(0, result.FailedCount);
        }
        finally
        {
            Cleanup(dbPath);
        }
    }

    private sealed class MockOptimizer : IAiListingOptimizer
    {
        public Task<ListingOptimizationResult> OptimizeAsync(ListingOptimizationInput input, CancellationToken ct = default)
        {
            return Task.FromResult(new ListingOptimizationResult(
                50,
                88,
                new[] { "AI Optimized: " + input.Title },
                new[] { "ai-tag-1", "ai-tag-2" },
                new[] { "Plastic" },
                "AI Description: " + input.Description,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
        }
    }
}
