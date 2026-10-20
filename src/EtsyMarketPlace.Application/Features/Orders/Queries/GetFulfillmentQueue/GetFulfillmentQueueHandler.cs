namespace EtsyMarketPlace.Application.Features.Orders.Queries.GetFulfillmentQueue;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Orders;
using MediatR;

public class GetFulfillmentQueueHandler(
    IEtsyOrderFulfillmentService fulfillmentService
) : IRequestHandler<GetFulfillmentQueueRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetFulfillmentQueueRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var orders = await fulfillmentService.GetFulfillmentQueueAsync(
            request.ShopId,
            request.LookbackDays,
            request.WasShipped,
            cancellationToken);

        var data = new
        {
            count = orders.Count,
            results = orders
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data);
    }
}
