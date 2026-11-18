using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetMySubscription;

public sealed class GetMySubscriptionHandler(
    ISubscriptionEntitlementService entitlementService)
    : IRequestHandler<GetMySubscriptionQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetMySubscriptionQuery request, CancellationToken cancellationToken)
    {
        var summary = await entitlementService.GetEntitlementSummaryAsync(
            request.UserId,
            request.RoleHint,
            request.UsernameHint,
            cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, summary);
    }
}
