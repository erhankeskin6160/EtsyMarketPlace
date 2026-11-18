using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;

public sealed class GetSubscriptionPlansHandler(
    ISubscriptionRepository subscriptionRepository)
    : IRequestHandler<GetSubscriptionPlansQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await subscriptionRepository.GetAllPlansAsync(request.ActiveOnly, cancellationToken);
        if (plans.Count == 0)
        {
            plans = SubscriptionFeatureKeys.GetDefaultPlans();
        }

        var dtos = plans
            .OrderBy(p => p.DisplayOrder)
            .Select(p => p.ToDto())
            .ToList();

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, dtos);
    }
}
