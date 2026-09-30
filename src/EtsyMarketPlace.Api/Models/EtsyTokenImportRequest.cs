namespace EtsyMarketPlace.Api.Models;

public sealed record EtsyTokenImportRequest(
    string ShopId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string TokenType = "Bearer");
