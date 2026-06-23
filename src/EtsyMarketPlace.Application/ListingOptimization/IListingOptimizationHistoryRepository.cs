namespace EtsyMarketPlace.Application.ListingOptimization;

public interface IListingOptimizationHistoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<ListingOptimizationHistoryEntry> SaveAsync(
        SaveListingOptimizationHistory history,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ListingOptimizationHistoryEntry>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
