using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetMySubscription;

public sealed record GetMySubscriptionQuery(
    string UserId,
    string? RoleHint = null,
    string? UsernameHint = null) : IRequest<ServiceResponse>;
