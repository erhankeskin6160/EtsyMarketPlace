using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetSubscriptionTransactions;

public sealed record GetSubscriptionTransactionsQuery(string UserId, int Limit = 50) : IRequest<ServiceResponse>;
