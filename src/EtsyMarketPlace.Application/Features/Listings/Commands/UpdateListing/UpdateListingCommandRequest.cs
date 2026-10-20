namespace EtsyMarketPlace.Application.Features.Listings.Commands.UpdateListing;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class UpdateListingCommandRequest : IRequest<ServiceResponse>
{
    public string ListingId { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public UpdateListingRequestDto Dto { get; set; } = null!;
}
