namespace EtsyMarketPlace.Application.Features.AiUsage.Queries.GetAiUsageStats;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAiUsageStatsRequest : IRequest<ServiceResponse>
{
    public string? Provider { get; set; }
    public string? Period { get; set; } = "30d";
}
