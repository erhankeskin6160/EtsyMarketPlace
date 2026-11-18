using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmCheckoutPayment;

public sealed record ConfirmCheckoutPaymentCommand(
    string UserId,
    string? RoleHint,
    string? UsernameHint,
    string? RemoteIpAddress,
    ConfirmVirtualPosPaymentRequest Request) : IRequest<ServiceResponse>;
