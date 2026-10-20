namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveFulfillmentMeta;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Orders;
using MediatR;

public class SaveFulfillmentMetaHandler(
    IEtsyOrderFulfillmentService fulfillmentService
) : IRequestHandler<SaveFulfillmentMetaRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SaveFulfillmentMetaRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        await fulfillmentService.SaveOrderAddressAsync(request.ReceiptId, request.UpdatedMeta!, cancellationToken);

        return ServiceResponse.CreateSuccess("Sipariş maliyetleri, paket ölçüleri ve GTİP kodu başarıyla PostgreSQL veritabanına kaydedildi.", new
        {
            success = true,
            receiptId = request.ReceiptId,
            message = "Sipariş maliyetleri, paket ölçüleri ve GTİP kodu başarıyla PostgreSQL veritabanına kaydedildi."
        });
    }
}
