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
