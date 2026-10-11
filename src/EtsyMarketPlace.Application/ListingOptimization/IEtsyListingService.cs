namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;

public interface IEtsyListingService
{
    Task<IReadOnlyList<ShopListingItemDto>> GetShopListingsAsync(
        string shopId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ListingUpdateResultDto> UpdateListingAsync(
        string shopId,
        string listingId,
        UpdateListingRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ListingUpdateResultDto> UpdateListingStateAsync(
        string shopId,
        long listingId,
        string state,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(
        string shopId,
        CancellationToken cancellationToken = default);
}
