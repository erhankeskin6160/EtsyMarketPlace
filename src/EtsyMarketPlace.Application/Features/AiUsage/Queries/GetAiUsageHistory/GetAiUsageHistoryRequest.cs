namespace EtsyMarketPlace.Application.Features.AiUsage.Queries.GetAiUsageHistory;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAiUsageHistoryRequest : IRequest<ServiceResponse>
{
    public string? Provider { get; set; }
    public string? Period { get; set; } = "30d";
    public int Limit { get; set; } = 100;
}
