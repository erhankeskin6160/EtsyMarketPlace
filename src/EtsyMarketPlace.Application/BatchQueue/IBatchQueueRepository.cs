namespace EtsyMarketPlace.Application.BatchQueue;

public interface IBatchQueueRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchQueueItem>> EnqueueBatchAsync(
        IReadOnlyList<SaveBatchQueueItem> items,
        CancellationToken cancellationToken = default);

    Task<BatchQueueItem?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchQueueItem>> GetPendingAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchQueueItem>> GetAllAsync(
        int limit = 500,
        CancellationToken cancellationToken = default);

    Task<BatchQueueItem?> UpdateItemAsync(
        BatchQueueItem item,
        CancellationToken cancellationToken = default);

    Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default);

    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
}
