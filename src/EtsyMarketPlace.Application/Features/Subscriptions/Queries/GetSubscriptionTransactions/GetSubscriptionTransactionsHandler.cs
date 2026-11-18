using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetSubscriptionTransactions;

public sealed class GetSubscriptionTransactionsHandler(
    ISubscriptionRepository subscriptionRepository)
    : IRequestHandler<GetSubscriptionTransactionsQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetSubscriptionTransactionsQuery request, CancellationToken cancellationToken)
    {
        var txs = await subscriptionRepository.GetTransactionsByUserAsync(
            request.UserId,
            Math.Clamp(request.Limit, 1, 200),
            cancellationToken);

        var dtos = txs.Select(t => t.ToDto()).ToList();
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, dtos);
    }
}
