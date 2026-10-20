namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Mağazaya ait Etsy OAuth v3 kimlik doğrulama belirteçleri.
/// </summary>
public class OAuthToken
{
    public string ShopId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string TokenType { get; set; } = "Bearer";

    public ShopConnection? Shop { get; set; }
}
