namespace EtsyMarketPlace.Application.AbTesting;

public interface IAbTestRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<ListingAbTestExperiment> SaveAsync(
        SaveAbTestExperiment experiment,
        CancellationToken cancellationToken = default);

    Task<ListingAbTestExperiment?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingAbTestExperiment>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingAbTestExperiment>> GetByListingIdAsync(
        string listingId,
        CancellationToken cancellationToken = default);

    Task<ListingAbTestExperiment?> UpdateMetricsAsync(
        UpdateAbTestMetrics update,
        CancellationToken cancellationToken = default);

    Task<ListingAbTestExperiment?> UpdateStatusAsync(
        long id,
        AbTestStatus status,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}
