namespace EtsyMarketPlace.Application.ListingOptimization;

public sealed class ListingOptimizationHistoryService(IListingOptimizationHistoryRepository repository)
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        repository.InitializeAsync(cancellationToken);

    public Task<ListingOptimizationHistoryEntry> SaveAsync(
        SaveListingOptimizationHistory history,
        CancellationToken cancellationToken = default) =>
        repository.SaveAsync(history, cancellationToken);

    public Task<IReadOnlyList<ListingOptimizationHistoryEntry>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        repository.GetRecentAsync(limit, cancellationToken);

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);
}
