namespace EtsyMarketPlace.Application.Features.Logistics.Queries.SearchHsCodes;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class SearchHsCodesRequest : IRequest<ServiceResponse>
{
    public string? Query { get; set; }
    public int Limit { get; set; } = 25;
}
