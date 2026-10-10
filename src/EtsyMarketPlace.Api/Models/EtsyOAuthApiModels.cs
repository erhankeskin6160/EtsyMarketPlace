namespace EtsyMarketPlace.Api.Models;

public sealed record ExchangeCodeApiRequest(
    string ShopId,
    string Code,
    string? State,
    string? CodeVerifier,
    string? RedirectUri);

public sealed record PkceSession(
    string CodeVerifier,
    string ShopId,
    string RedirectUri,
    DateTimeOffset CreatedAt);

public sealed record OptimizeListingApiRequest(
    string? ShopId = null,
    string? Title = null,
    string? Description = null,
    IReadOnlyList<string>? Tags = null,
    string? TargetKeyword = null,
    string? DescriptionStyle = "Storytelling",
    string? Model = null,
    string? Tone = null,
    string? FocusKeywords = null,
    string? TargetBuyerPersona = null,
    string? ApiKey = null,
    string? Provider = null);

public sealed record UpdateListingApiRequest(
    string ShopId,
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string>? Materials = null);

public sealed record CreateListingApiRequest(
    string? ShopId,
    string Title,
    string Description,
    decimal Price,
    int Quantity,
    long TaxonomyId,
    long? ShippingProfileId = null,
    long? ReadinessStateId = null,
    bool IsDigital = false,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? Materials = null,
    string WhoMade = "i_did",
    string WhenMade = "made_to_order",
    string State = "draft",
    IReadOnlyList<ListingImageDto>? Images = null,
    IReadOnlyList<ListingVariationDto>? Variations = null,
    IReadOnlyList<ListingVariationGroupDto>? VariationGroups = null);

public sealed record ListingImageDto(
    string? DataUrl,
    string? Url,
    int Rank = 1);

public sealed record ListingVariationDto(
    string Key,
    decimal Price,
    int Quantity,
    bool Active = true);

public sealed record ListingVariationGroupDto(
    string Name,
    IReadOnlyList<string> Values);

public sealed record EtsyTaxonomyNodeDto(
    long Id,
    string Name,
    string Path);

public sealed record SaveAiSettingsApiRequest(
    string? ShopId,
    string SettingsJson);

