namespace EtsyMarketPlace.Application.Features.AbTests.Commands.CreateAbTest;

using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class CreateAbTestRequest : IRequest<ServiceResponse>
{
    public SaveAbTestExperiment Experiment { get; set; } = null!;
}
