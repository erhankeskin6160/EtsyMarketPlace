namespace EtsyMarketPlace.Application.Features.Tracking.Queries.GetTrackingItems;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Tracking;
using MediatR;

public class GetTrackingItemsHandler(
    ITrackingRepository repository
) : IRequestHandler<GetTrackingItemsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetTrackingItemsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var items = await repository.GetItemsAsync(cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, items);
    }
}
