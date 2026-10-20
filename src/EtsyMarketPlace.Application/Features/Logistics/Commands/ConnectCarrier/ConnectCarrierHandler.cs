namespace EtsyMarketPlace.Application.Features.Logistics.Commands.ConnectCarrier;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class ConnectCarrierHandler(
    IShipmentOrchestratorService orchestratorService
) : IRequestHandler<ConnectCarrierRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ConnectCarrierRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var result = await orchestratorService.ConnectCarrierAsync(request.Command, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, result);
    }
}
