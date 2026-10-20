namespace EtsyMarketPlace.Application.Features.AiUsage.Queries.GetAiUsageHistory;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAiUsageHistoryHandler(
    IAiUsageRepository repository
) : IRequestHandler<GetAiUsageHistoryRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetAiUsageHistoryRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var (since, until) = AiPeriodHelper.ResolveDateRange(request.Period);
        int clampedLimit = Math.Clamp(request.Limit <= 0 ? 100 : request.Limit, 1, 1000);
        var history = await repository.GetHistoryAsync(request.Provider, since, clampedLimit, until, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, history);
    }
}
