namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;

public sealed record EtsyShippingProfileDto(
    long ShippingProfileId,
    string Title,
    int? MinProcessingDays,
    int? MaxProcessingDays,
    string? ProcessingDaysDisplay
);

public sealed record EtsyReadinessStateDto(
    long ReadinessStateId,
    string Title,
    string? ReadinessState,
    int MinProcessingTime,
    int MaxProcessingTime,
    string ProcessingTimeUnit
);

public sealed record EtsyTaxonomyNodeItemDto(
    long Id,
    string Name,
    string Path
);
