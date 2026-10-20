namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateCarrierShipment;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class CreateCarrierShipmentHandler(
    IShipmentOrchestratorService orchestratorService
) : IRequestHandler<CreateCarrierShipmentRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(CreateCarrierShipmentRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        try
        {
            var result = await orchestratorService.CreateShipmentAsync(request.Command, cancellationToken);
            if (!result.Success)
            {
                return ServiceResponse.CreateError(result.ErrorMessage ?? "Gönderi oluşturulamadı.", HttpStatusCode.BadRequest);
            }

            return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, result);
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError($"Gönderi oluşturulurken sunucu hatası oluştu: {ex.Message}", HttpStatusCode.BadRequest);
        }
    }
}
