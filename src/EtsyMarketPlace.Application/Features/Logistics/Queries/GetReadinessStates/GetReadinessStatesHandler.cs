namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetReadinessStates;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class GetReadinessStatesHandler(
    IEtsyLogisticsService logisticsService
) : IRequestHandler<GetReadinessStatesRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetReadinessStatesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var states = await logisticsService.GetReadinessStatesAsync(request.ShopId, cancellationToken);

        var data = new
        {
            count = states.Count,
            results = states
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data);
    }
}
