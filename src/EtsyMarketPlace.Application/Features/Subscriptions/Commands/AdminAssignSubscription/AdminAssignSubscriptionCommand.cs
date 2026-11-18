using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminAssignSubscription;

public sealed record AdminAssignSubscriptionCommand(
    string TargetUserId,
    string? AdminId,
    string? AdminName,
    string? RemoteIpAddress,
    AdminAssignSubscriptionRequest Request) : IRequest<ServiceResponse>;
