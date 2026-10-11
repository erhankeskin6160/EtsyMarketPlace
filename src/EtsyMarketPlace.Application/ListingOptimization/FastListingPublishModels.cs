namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Collections.Generic;

public sealed record ListingImageInput(
    string? DataUrl,
    string? Url,
    int Rank
);

public sealed record ListingVariationInput(
    string Key,
    decimal Price,
    int Quantity,
    bool Active
);

public sealed record ListingVariationGroupInput(
    string Name,
    IReadOnlyList<string> Values
);

public sealed record FastListingPublishCommand(
    string? ShopId,
    string Title,
    string Description,
    decimal Price,
    int Quantity,
    long TaxonomyId,
    long? ShippingProfileId,
    long? ReadinessStateId,
    bool IsDigital,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<string>? Materials,
    string State,
    string WhoMade = "i_did",
    string WhenMade = "made_to_order",
    IReadOnlyList<ListingImageInput>? Images = null,
    IReadOnlyList<ListingVariationInput>? Variations = null,
    IReadOnlyList<ListingVariationGroupInput>? VariationGroups = null
);

public sealed record FastListingPublishResult(
    bool Success,
    long ListingId,
    string Url,
    string State,
    int UploadedImages,
    int VariationsCount,
    string? VariationError,
    string Message
);

public sealed record CreateDraftListingRawRequest(
    string Title,
    string Description,
    decimal Price,
    int Quantity,
    long TaxonomyId,
    string Type,
    string State,
    string WhoMade,
    string WhenMade,
    long? ShippingProfileId,
    long? ReadinessStateId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Materials
);

public sealed class EtsyInventoryPayload
{
    public required List<EtsyInventoryProduct> Products { get; init; }
    public required long[] PriceOnProperty { get; init; }
    public long[] QuantityOnProperty { get; init; } = [];
    public long[] SkuOnProperty { get; init; } = [];
}

public sealed class EtsyInventoryProduct
{
    public required string Sku { get; init; }
    public required List<EtsyInventoryPropertyValue> PropertyValues { get; init; }
    public required List<EtsyInventoryOffering> Offerings { get; init; }
}

public sealed class EtsyInventoryPropertyValue
{
    public required long PropertyId { get; init; }
    public required string PropertyName { get; init; }
    public long? ScaleId { get; init; }
    public long[] ValueIds { get; init; } = [];
    public required string[] Values { get; init; }
}

public sealed class EtsyInventoryOffering
{
    public required double Price { get; init; }
    public required int Quantity { get; init; }
    public required bool IsEnabled { get; init; }
    public long? ReadinessStateId { get; init; }
}
