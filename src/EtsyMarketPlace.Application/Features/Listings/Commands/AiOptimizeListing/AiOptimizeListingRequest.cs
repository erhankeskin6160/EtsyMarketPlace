namespace EtsyMarketPlace.Application.Features.Listings.Commands.AiOptimizeListing;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

public class AiOptimizeListingRequest : IRequest<ServiceResponse>
{
    public string ListingId { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public OptimizeListingCommandDto Command { get; set; } = null!;
}
