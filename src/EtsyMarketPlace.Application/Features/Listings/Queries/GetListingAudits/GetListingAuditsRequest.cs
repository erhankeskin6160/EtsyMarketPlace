namespace EtsyMarketPlace.Application.Features.Listings.Queries.GetListingAudits;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetListingAuditsRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
}
