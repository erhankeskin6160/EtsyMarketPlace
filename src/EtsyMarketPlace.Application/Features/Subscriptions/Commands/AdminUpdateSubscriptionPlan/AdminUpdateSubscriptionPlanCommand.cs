using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdateSubscriptionPlan;

public sealed record AdminUpdateSubscriptionPlanCommand(
    string PlanId,
    string? AdminId,
    string? AdminName,
    string? RemoteIpAddress,
    AdminUpdateSubscriptionPlanRequest Request) : IRequest<ServiceResponse>;
