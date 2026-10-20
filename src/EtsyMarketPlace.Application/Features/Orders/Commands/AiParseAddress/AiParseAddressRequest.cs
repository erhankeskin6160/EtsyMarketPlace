namespace EtsyMarketPlace.Application.Features.Orders.Commands.AiParseAddress;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class AiParseAddressRequest : IRequest<ServiceResponse>
{
    public string RawAddress { get; set; } = string.Empty;
}
