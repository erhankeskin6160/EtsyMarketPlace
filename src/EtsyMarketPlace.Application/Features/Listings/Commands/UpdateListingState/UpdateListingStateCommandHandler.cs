namespace EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListingState;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class UpdateListingStateCommandHandler(
    IEtsyListingService listingService
) : IRequestHandler<UpdateListingStateCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpdateListingStateCommandRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var result = await listingService.UpdateListingStateAsync(request.ShopId, request.ListingId, request.State, cancellationToken);
        return ServiceResponse.CreateSuccess("İlan durumu güncellendi.", new
        {
            success = true,
            listingId = request.ListingId,
            state = result.State,
            message = result.Message
        });
    }
}
