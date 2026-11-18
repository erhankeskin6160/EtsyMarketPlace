using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Listings.Commands.RunVariationPricingAgent;

public sealed record RunVariationPricingAgentCommand(VariationPricingAgentRequestDto Request) : IRequest<ServiceResponse>;
