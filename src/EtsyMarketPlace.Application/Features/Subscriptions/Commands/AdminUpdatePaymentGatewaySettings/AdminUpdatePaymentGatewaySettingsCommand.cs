using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdatePaymentGatewaySettings;

public sealed record AdminUpdatePaymentGatewaySettingsCommand(
    string? AdminId,
    string? AdminName,
    string? RemoteIpAddress,
    UpdatePaymentGatewaySettingsRequest Request) : IRequest<ServiceResponse>;
