namespace EtsyMarketPlace.Application.Features.Listings.Queries.GetShopListings;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetShopListingsRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public int Limit { get; set; } = 50;
}
