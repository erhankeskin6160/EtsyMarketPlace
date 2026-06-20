namespace SimilarProductsWinForms.Models;

internal sealed class EtsyApiSettings
{
    public string Keystring { get; set; } = "";

    public string SharedSecret { get; set; } = "";

    public string RedirectUri { get; set; } = "https://www.example.com/oauth/etsy";

    public string AccessToken { get; set; } = "";

    public string RefreshToken { get; set; } = "";

    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }

    public string LastCodeVerifier { get; set; } = "";

    public string LastState { get; set; } = "";

    public bool HasApiCredentials =>
        !string.IsNullOrWhiteSpace(Keystring) &&
        !string.IsNullOrWhiteSpace(SharedSecret);

    public bool HasAccessToken =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2);

    public string ApiKeyHeader => $"{Keystring.Trim()}:{SharedSecret.Trim()}";
}
