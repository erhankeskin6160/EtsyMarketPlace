namespace EtsyMarketPlace.Application.Features.AbTests.Commands.UpdateAbTestStatus;

using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class UpdateAbTestStatusRequest : IRequest<ServiceResponse>
{
    public long Id { get; set; }
    public AbTestStatus Status { get; set; }
}
