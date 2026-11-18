using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.CheckSubscriptionFeature;

public sealed class CheckSubscriptionFeatureHandler(
    ISubscriptionEntitlementService entitlementService)
    : IRequestHandler<CheckSubscriptionFeatureQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(CheckSubscriptionFeatureQuery request, CancellationToken cancellationToken)
    {
        var check = await entitlementService.CheckFeatureAccessAsync(
            request.UserId,
            request.FeatureKey,
            request.RoleHint,
            cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, check);
    }
}
