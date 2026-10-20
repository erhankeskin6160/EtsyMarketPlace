namespace EtsyMarketPlace.Application.Features.Tracking.Commands.DeleteTrackingItem;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Tracking;
using MediatR;

public class DeleteTrackingItemHandler(
    ITrackingRepository repository
) : IRequestHandler<DeleteTrackingItemRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(DeleteTrackingItemRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        await repository.DeleteItemAsync(request.Id, cancellationToken);
        return ServiceResponse.CreateSuccess("Takip öğesi başarıyla silindi.", new { success = true });
    }
}
