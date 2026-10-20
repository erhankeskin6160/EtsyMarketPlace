namespace EtsyMarketPlace.Application.Features.AbTests.Commands.DeleteAbTest;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class DeleteAbTestRequest : IRequest<ServiceResponse>
{
    public long Id { get; set; }
}
