using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Listings.Commands.SuggestListingVariations;

public sealed record SuggestListingVariationsCommand(SuggestListingVariationsRequestDto Request) : IRequest<ServiceResponse>;
