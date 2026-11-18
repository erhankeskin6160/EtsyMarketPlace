using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ProcessPaymentCallback;

public sealed record ProcessPaymentCallbackCommand(
    string Provider,
    PaytrCallbackInput? PaytrInput,
    IyzicoCallbackInput? IyzicoInput,
    string? RemoteIpAddress) : IRequest<ServiceResponse>;
