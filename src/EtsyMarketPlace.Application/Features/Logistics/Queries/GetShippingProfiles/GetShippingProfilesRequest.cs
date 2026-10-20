namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetShippingProfiles;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetShippingProfilesRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
}
