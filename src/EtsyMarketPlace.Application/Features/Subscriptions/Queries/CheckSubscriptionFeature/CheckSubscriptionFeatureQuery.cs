using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.CheckSubscriptionFeature;

public sealed record CheckSubscriptionFeatureQuery(
    string UserId,
    string FeatureKey,
    string? RoleHint = null) : IRequest<ServiceResponse>;
