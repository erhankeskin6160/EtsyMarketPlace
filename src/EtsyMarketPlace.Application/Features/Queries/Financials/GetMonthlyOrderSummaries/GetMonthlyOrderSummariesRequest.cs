using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Queries.Financials.GetMonthlyOrderSummaries;

public class GetMonthlyOrderSummariesRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public int Months { get; set; } = 12;
}
