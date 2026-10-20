namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetReadinessStates;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetReadinessStatesRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
}
