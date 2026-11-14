namespace EtsyMarketPlace.Application.Features.Logistics.Commands.CreateReturnPolicy;

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class CreateReturnPolicyHandler(
    IEtsyLogisticsService logisticsService
) : IRequestHandler<CreateReturnPolicyRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(CreateReturnPolicyRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        try
        {
            var result = await logisticsService.CreateReturnPolicyAsync(request.Command, cancellationToken);
            if (!result.Success || result.Policy == null)
            {
                return ServiceResponse.CreateError(result.ErrorMessage ?? "Etsy iade politikası oluşturulamadı.", HttpStatusCode.BadRequest);
            }

            return ServiceResponse<EtsyReturnPolicyDto>.CreateSuccess(ResponseMessage.Succeed, result.Policy);
        }
        catch (Exception ex)
        {
            return ServiceResponse.CreateError($"Etsy iade politikası oluşturulurken hata oluştu: {ex.Message}", HttpStatusCode.BadRequest);
        }
    }
}
