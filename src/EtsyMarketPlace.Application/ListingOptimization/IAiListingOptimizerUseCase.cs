namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Threading;
using System.Threading.Tasks;

public interface IAiListingOptimizerUseCase
{
    Task<OptimizeListingResultDto> OptimizeListingAsync(
        string listingId,
        string shopId,
        OptimizeListingCommandDto command,
        CancellationToken cancellationToken = default);
}
