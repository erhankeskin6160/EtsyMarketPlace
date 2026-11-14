namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateReturnPolicy;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class CreateReturnPolicyRequest : IRequest<ServiceResponse>
{
    public CreateEtsyReturnPolicyCommand Command { get; set; } = new(null, true, true, 30);
}
