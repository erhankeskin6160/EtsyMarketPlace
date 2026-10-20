namespace EtsyMarketPlace.Application.Features.Listings.Queries.GetShopListings;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class GetShopListingsHandler(
    IEtsyListingService listingService
) : IRequestHandler<GetShopListingsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetShopListingsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var items = await listingService.GetShopListingsAsync(request.ShopId, request.Limit, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, items);
    }
}
