namespace EtsyMarketPlace.Application.Features.AiUsage.Commands.RecordAiUsage;

using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class RecordAiUsageRequest : IRequest<ServiceResponse>
{
    public AiUsageRecord Record { get; set; } = null!;
}
