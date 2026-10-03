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
    string ShopId,
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    string? TargetKeyword = null,
    string? DescriptionStyle = "Storytelling");

public sealed record UpdateListingApiRequest(
    string ShopId,
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string>? Materials = null);
