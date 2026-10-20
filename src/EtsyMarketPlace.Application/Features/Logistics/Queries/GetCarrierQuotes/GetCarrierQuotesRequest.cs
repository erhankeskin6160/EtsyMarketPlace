namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetCarrierQuotes;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

public class GetCarrierQuotesRequest : IRequest<ServiceResponse>
{
    public CarrierQuotesQuery Query { get; set; } = new();
}
