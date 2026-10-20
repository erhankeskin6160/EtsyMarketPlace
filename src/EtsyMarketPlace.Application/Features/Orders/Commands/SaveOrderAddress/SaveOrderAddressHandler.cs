namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveOrderAddress;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Orders;
using MediatR;

public class SaveOrderAddressHandler(
    IEtsyOrderFulfillmentService fulfillmentService
) : IRequestHandler<SaveOrderAddressRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(SaveOrderAddressRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        await fulfillmentService.SaveOrderAddressAsync(request.ReceiptId, request.UpdatedAddress!, cancellationToken);

        return ServiceResponse.CreateSuccess("Alıcı adresi ve sipariş bilgileri başarıyla kaydedildi.", new
        {
            success = true,
            receiptId = request.ReceiptId
        });
    }
}
