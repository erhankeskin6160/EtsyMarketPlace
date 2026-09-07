namespace EtsyMarketPlace.Application.ListingOptimization;

public interface IAiListingOptimizer
{
    Task<ListingOptimizationResult> OptimizeAsync(
        ListingOptimizationInput input,
        CancellationToken cancellationToken = default);
}
