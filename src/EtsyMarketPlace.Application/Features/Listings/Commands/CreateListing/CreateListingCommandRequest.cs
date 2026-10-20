namespace EtsyMarketPlace.Application.Features.Listings.Commands.CreateListing;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class CreateListingCommandRequest : IRequest<ServiceResponse>
{
    public FastListingPublishCommand Command { get; set; } = null!;
}
