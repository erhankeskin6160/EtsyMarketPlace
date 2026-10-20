namespace EtsyMarketPlace.Application.Features.AiUsage.Queries.GetAiUsageStats;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAiUsageStatsHandler(
    IAiUsageRepository repository
) : IRequestHandler<GetAiUsageStatsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetAiUsageStatsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var (since, until) = AiPeriodHelper.ResolveDateRange(request.Period);
        var stats = await repository.GetSummaryStatsAsync(request.Provider, since, until, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, stats);
    }
}
