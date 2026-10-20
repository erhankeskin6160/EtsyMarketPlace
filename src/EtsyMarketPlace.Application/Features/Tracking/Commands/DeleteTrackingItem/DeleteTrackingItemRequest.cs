namespace EtsyMarketPlace.Application.Features.Tracking.Commands.DeleteTrackingItem;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class DeleteTrackingItemRequest : IRequest<ServiceResponse>
{
    public long Id { get; set; }
}
