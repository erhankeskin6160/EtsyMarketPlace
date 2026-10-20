namespace EtsyMarketPlace.Application.Features.Logistics.Queries.SearchHsCodes;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class SearchHsCodesHandler(
    IShipmentOrchestratorService orchestratorService
) : IRequestHandler<SearchHsCodesRequest, ServiceResponse>
{
    public Task<ServiceResponse> Handle(SearchHsCodesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout));

        var items = orchestratorService.SearchHsCodes(request.Query, request.Limit);

        var data = new
        {
            count = items.Count,
            results = items
        };

        return Task.FromResult(ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data));
    }
}
