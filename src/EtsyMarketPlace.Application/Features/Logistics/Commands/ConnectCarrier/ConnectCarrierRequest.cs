namespace EtsyMarketPlace.Application.Features.Logistics.Commands.ConnectCarrier;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class ConnectCarrierRequest : IRequest<ServiceResponse>
{
    public ConnectCarrierCommand Command { get; set; } = new();
}
