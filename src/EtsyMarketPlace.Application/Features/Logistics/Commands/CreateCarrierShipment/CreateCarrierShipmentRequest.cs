namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateCarrierShipment;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class CreateCarrierShipmentRequest : IRequest<ServiceResponse>
{
    public CreateCarrierShipmentCommand Command { get; set; } = new();
}
