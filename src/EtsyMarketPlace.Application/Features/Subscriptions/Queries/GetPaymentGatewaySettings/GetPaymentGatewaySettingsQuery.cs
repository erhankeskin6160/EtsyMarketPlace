using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetPaymentGatewaySettings;

public sealed record GetPaymentGatewaySettingsQuery : IRequest<ServiceResponse>;
