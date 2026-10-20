namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierQuotes;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class GetCarrierQuotesHandler(
    IShipmentOrchestratorService orchestratorService
) : IRequestHandler<GetCarrierQuotesRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetCarrierQuotesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var result = await orchestratorService.GetCarrierQuotesAsync(request.Query, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, result);
    }
}
