using System;
using System.Collections.Generic;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;

public class ImportFullFinancialsRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public List<EtsyFinancialTransaction>? Transactions { get; set; }
    public List<EtsyBankPayout>? Payouts { get; set; }
    public List<EtsyOrderCostAlert>? OrderAlerts { get; set; }
    public List<EtsyMonthlyOrderSummary>? MonthlyOrders { get; set; }
    public List<EtsyListingTrafficRecord>? TrafficRecords { get; set; }
    public List<EtsyChartSnapshot>? ChartPayloads { get; set; }
    public DateTimeOffset? PeriodStart { get; set; }
    public DateTimeOffset? PeriodEnd { get; set; }
}
