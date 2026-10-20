namespace EtsyMarketPlace.Application.Features.Logistics.Queries.SearchSellerTaxonomyNodes;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class SearchSellerTaxonomyNodesRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 100;
}
