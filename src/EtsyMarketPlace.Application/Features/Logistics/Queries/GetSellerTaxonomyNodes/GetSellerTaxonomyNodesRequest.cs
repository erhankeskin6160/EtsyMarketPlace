namespace EtsyMarketPlace.Application.Features.Logistics.Queries.GetSellerTaxonomyNodes;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetSellerTaxonomyNodesRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
}
