namespace EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListingState;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class UpdateListingStateCommandRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public long ListingId { get; set; }
    public string State { get; set; } = string.Empty;
}
