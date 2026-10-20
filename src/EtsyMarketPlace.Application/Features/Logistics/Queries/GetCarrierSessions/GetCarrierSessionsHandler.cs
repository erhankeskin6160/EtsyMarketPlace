namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierSessions;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class GetCarrierSessionsHandler(
    IShipmentOrchestratorService orchestratorService
) : IRequestHandler<GetCarrierSessionsRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetCarrierSessionsRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var sessions = await orchestratorService.GetCarrierSessionsAsync(cancellationToken);

        var data = new
        {
            count = sessions.Count,
            results = sessions
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data);
    }
}
