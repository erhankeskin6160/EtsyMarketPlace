namespace EtsyMarketPlace.Application.Features.Listings.Queries.GetListingAudits;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class GetListingAuditsHandler(
    IEtsyListingService listingService
) : IRequestHandler<GetListingAuditsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetListingAuditsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var audits = await listingService.GetListingAuditsAsync(request.ShopId, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, audits);
    }
}
