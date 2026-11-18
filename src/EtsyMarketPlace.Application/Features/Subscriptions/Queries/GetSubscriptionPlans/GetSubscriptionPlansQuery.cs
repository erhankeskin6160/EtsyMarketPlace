using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;

public sealed record GetSubscriptionPlansQuery(bool ActiveOnly = true) : IRequest<ServiceResponse>;
