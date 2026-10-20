using System;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Queries.Financials.GetBankPayouts;

public class GetBankPayoutsRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}
