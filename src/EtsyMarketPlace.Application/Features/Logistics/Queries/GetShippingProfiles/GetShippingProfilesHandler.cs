namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetShippingProfiles;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class GetShippingProfilesHandler(
    IEtsyLogisticsService logisticsService
) : IRequestHandler<GetShippingProfilesRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetShippingProfilesRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var profiles = await logisticsService.GetShippingProfilesAsync(request.ShopId, cancellationToken);

        var data = new
        {
            count = profiles.Count,
            results = profiles
        };

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, data);
    }
}
