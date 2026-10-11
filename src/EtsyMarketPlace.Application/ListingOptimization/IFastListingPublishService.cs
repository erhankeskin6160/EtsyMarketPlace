namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Threading;
using System.Threading.Tasks;

public interface IFastListingPublishService
{
    Task<FastListingPublishResult> PublishListingAsync(
        FastListingPublishCommand command,
        CancellationToken cancellationToken = default);
}
