namespace EtsyMarketPlace.Api.Models;

public sealed record EtsySyncRequest(
    string? ShopId = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null);
