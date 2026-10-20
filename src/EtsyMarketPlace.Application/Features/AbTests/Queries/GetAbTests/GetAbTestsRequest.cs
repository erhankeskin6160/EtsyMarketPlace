namespace EtsyMarketPlace.Application.Features.AbTests.Queries.GetAbTests;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetAbTestsRequest : IRequest<ServiceResponse>
{
    public string? ListingId { get; set; }
    public int Limit { get; set; } = 50;
}
