using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using EtsyMarketPlace.Application.EtsyIntegration;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyOAuthService(HttpClient httpClient, IOptions<EtsyApiOptions> options) : IEtsyOAuthService
{
    private readonly EtsyApiOptions _options = options.Value;

    public Task<string> CreateAuthorizationUrlAsync(string userId, string state, CancellationToken cancellationToken = default)
    {
        var codeChallenge = Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ApiKey,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = string.Join(' ', _options.Scopes),
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };
        var url = _options.AuthorizationBaseUrl + "?" + string.Join('&', query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        return Task.FromResult(url);
    }

    public async Task<EtsyOAuthToken> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _options.ApiKey,
                ["redirect_uri"] = _options.RedirectUri,
                ["code"] = code,
                ["code_verifier"] = codeVerifier
            })
        };
        return await SendTokenRequestAsync(request, cancellationToken);
    }

    public async Task<EtsyOAuthToken> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _options.ApiKey,
                ["refresh_token"] = refreshToken
            })
        };
        return await SendTokenRequestAsync(request, cancellationToken);
    }

    private async Task<EtsyOAuthToken> SendTokenRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Etsy OAuth sunucu hatası (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        var token = await response.Content.ReadFromJsonAsync<EtsyTokenResponse>(cancellationToken);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken))
            throw new InvalidOperationException("Etsy OAuth yanıtı geçersiz veya eksik token içeriyor.");
        return new EtsyOAuthToken(token.AccessToken, token.RefreshToken, DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn), token.TokenType ?? "Bearer");
    }

    public static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class EtsyTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    }
}