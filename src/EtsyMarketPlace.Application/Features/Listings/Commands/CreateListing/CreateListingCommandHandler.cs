namespace EtsyMarketPlace.Application.Features.Listings.Commands.CreateListing;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class CreateListingCommandHandler(
    IFastListingPublishService publishService
) : IRequestHandler<CreateListingCommandRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(CreateListingCommandRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var result = await publishService.PublishListingAsync(request.Command, cancellationToken);
        if (!result.Success)
        {
            return ServiceResponse.CreateError(result.Message, HttpStatusCode.BadRequest);
        }

        return ServiceResponse.CreateSuccess("İlan başarıyla oluşturuldu.", result);
    }
}
