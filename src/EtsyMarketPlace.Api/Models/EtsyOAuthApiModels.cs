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
