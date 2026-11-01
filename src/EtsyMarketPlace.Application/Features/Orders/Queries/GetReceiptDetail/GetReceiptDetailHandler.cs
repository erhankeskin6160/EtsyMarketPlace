namespace EtsyMarketPlace.Application.Features.Orders.Queries.GetReceiptDetail;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Orders;
using MediatR;

public class GetReceiptDetailHandler(
    IEtsyOrderFulfillmentService fulfillmentService
) : IRequestHandler<GetReceiptDetailRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetReceiptDetailRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.ReceiptId <= 0)
            return ServiceResponse.CreateError("Geçersiz sipariş (receipt) numarası.", HttpStatusCode.BadRequest);

        var order = await fulfillmentService.GetReceiptDetailAsync(
            request.ShopId,
            request.ReceiptId,
            cancellationToken);

        if (order == null)
            return ServiceResponse.CreateError("Sipariş detayı Etsy API'den alınamadı.", HttpStatusCode.NotFound);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, order);
    }
}
