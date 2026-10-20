namespace EtsyMarketPlace.Application.Features.Tracking.Queries.GetTrackingSnapshots;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Tracking;
using MediatR;

public class GetTrackingSnapshotsHandler(
    ITrackingRepository repository
) : IRequestHandler<GetTrackingSnapshotsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetTrackingSnapshotsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var snapshots = await repository.GetSnapshotsAsync(request.TrackingItemId, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, snapshots);
    }
}
