namespace EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListing;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class UpdateListingCommandHandler(
    IEtsyListingService listingService
) : IRequestHandler<UpdateListingCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpdateListingCommandRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var result = await listingService.UpdateListingAsync(request.ShopId, request.ListingId, request.Dto, cancellationToken);
        return ServiceResponse.CreateSuccess("İlan başarıyla güncellendi.", new
        {
            success = true,
            listingId = result.ListingId,
            message = result.Message
        });
    }
}
