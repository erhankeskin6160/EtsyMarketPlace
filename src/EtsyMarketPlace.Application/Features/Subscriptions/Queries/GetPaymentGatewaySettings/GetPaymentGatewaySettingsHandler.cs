using System.Net;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetPaymentGatewaySettings;

public sealed class GetPaymentGatewaySettingsHandler(ISubscriptionPaymentGateway paymentGateway)
    : IRequestHandler<GetPaymentGatewaySettingsQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetPaymentGatewaySettingsQuery request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var settings = await paymentGateway.GetSettingsSummaryAsync(cancellationToken);
        return ServiceResponse.CreateSuccess("Ödeme altyapısı ayarları getirildi.", settings);
    }
}
