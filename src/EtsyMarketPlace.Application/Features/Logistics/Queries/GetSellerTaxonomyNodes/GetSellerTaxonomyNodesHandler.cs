namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetSellerTaxonomyNodes;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class GetSellerTaxonomyNodesHandler(
    IEtsyLogisticsService logisticsService
) : IRequestHandler<GetSellerTaxonomyNodesRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetSellerTaxonomyNodesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var nodes = await logisticsService.GetSellerTaxonomyNodesAsync(request.ShopId, cancellationToken);

        var data = new
        {
            count = nodes.Count,
            results = nodes
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data);
    }
}
