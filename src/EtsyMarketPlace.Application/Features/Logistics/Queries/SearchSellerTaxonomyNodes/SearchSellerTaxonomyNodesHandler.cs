namespace EtsyMarketPlace.Application.Features.Logistics.Queries.SearchSellerTaxonomyNodes;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class SearchSellerTaxonomyNodesHandler(
    IEtsyLogisticsService logisticsService
) : IRequestHandler<SearchSellerTaxonomyNodesRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SearchSellerTaxonomyNodesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var nodes = await logisticsService.SearchSellerTaxonomyNodesAsync(
            request.ShopId,
            request.Query,
            request.Limit,
            cancellationToken);

        var data = new
        {
            count = nodes.Count,
            results = nodes
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data);
    }
}
