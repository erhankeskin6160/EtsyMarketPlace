namespace EtsyMarketPlace.Application.Features.Orders.Queries.GetFulfillmentQueue;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetFulfillmentQueueRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public int LookbackDays { get; set; } = 45;
    public bool? WasShipped { get; set; }
}
