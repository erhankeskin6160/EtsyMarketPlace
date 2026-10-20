namespace EtsyMarketPlace.Application.Features.Tracking.Queries.GetTrackingSnapshots;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetTrackingSnapshotsRequest : IRequest<ServiceResponse>
{
    public long TrackingItemId { get; set; }
}
