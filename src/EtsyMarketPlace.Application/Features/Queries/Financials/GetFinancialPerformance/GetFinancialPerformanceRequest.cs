using System;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Queries.Financials.GetFinancialPerformance;

public class GetFinancialPerformanceRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public string Period { get; set; } = "this_month";
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}
