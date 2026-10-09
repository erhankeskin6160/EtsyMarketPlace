namespace EtsyMarketPlace.Api.Models;

public sealed record EtsyUpdateOrderCostRequest(
    decimal? ProductCost,
    decimal? ShippingCost,
    decimal? PackagingCost = null,
    string? Notes = null);
