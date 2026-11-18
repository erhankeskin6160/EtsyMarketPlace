using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;

public sealed record CreateCheckoutSessionCommand(
    string UserId,
    string? RoleHint,
    string? UsernameHint,
    string? EmailHint,
    CreateCheckoutSessionRequest Request,
    string? ClientIp = null) : IRequest<ServiceResponse>;
