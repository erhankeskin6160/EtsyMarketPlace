namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetReturnPolicies;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetReturnPoliciesRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
}
