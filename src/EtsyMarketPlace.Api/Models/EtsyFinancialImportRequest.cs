using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Api.Models;

public sealed record EtsyFinancialImportRequest(
    string ShopId,
    List<EtsyFinancialTransaction>? Transactions,
    List<EtsyBankPayout>? Payouts,
    List<EtsyOrderCostAlert>? OrderAlerts,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd);
