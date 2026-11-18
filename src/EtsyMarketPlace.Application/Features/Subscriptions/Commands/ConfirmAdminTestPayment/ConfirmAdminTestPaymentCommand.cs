using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmAdminTestPayment;

public sealed record ConfirmAdminTestPaymentCommand(
    string SessionId,
    string UserId,
    string? RoleHint,
    string? UsernameHint,
    string? RemoteIpAddress) : IRequest<ServiceResponse>;
