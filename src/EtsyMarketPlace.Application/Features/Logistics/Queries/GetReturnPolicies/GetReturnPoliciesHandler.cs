namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetReturnPolicies;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class GetReturnPoliciesHandler(
    IEtsyLogisticsService logisticsService
) : IRequestHandler<GetReturnPoliciesRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetReturnPoliciesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var policies = await logisticsService.GetReturnPoliciesAsync(request.ShopId, cancellationToken);

        var data = new
        {
            count = policies.Count,
            results = policies
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data);
    }
}
