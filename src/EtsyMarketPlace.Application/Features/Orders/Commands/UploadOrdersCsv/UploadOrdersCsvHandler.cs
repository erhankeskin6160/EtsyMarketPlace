namespace EtsyMarketPlace.Application.Features.Orders.Commands.UploadOrdersCsv;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Domain.Orders;
using MediatR;

public class UploadOrdersCsvHandler(
    IOrderAddressRepository addressRepository
) : IRequestHandler<UploadOrdersCsvRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UploadOrdersCsvRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (request.Records.Count == 0)
            return ServiceResponse.CreateError("Yüklenecek sipariş adresi kaydı bulunamadı.", HttpStatusCode.BadRequest);

        await addressRepository.SaveBatchAsync(request.Records, cancellationToken);

        return ServiceResponse.CreateSuccess($"{request.Records.Count} sipariş teslimat adresi başarıyla PostgreSQL veritabanına kaydedildi.", new
        {
            success = true,
            count = request.Records.Count
        });
    }
}
