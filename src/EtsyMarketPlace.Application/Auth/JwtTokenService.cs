using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EtsyMarketPlace.Application.Auth;

public sealed class JwtTokenService
{
    private readonly byte[] _key;
    private readonly string _issuer = "EtsyMarketPlace.Api";
    private readonly string _audience = "EtsyMarketPlace.Web";

    public JwtTokenService(string? secret = null)
    {
        var validSecret = string.IsNullOrWhiteSpace(secret)
            ? "EtsyMarketPlace_Enterprise_Secure_Secret_Key_2026_Gemini_Spark_JWT!"
            : secret;
        _key = Encoding.UTF8.GetBytes(validSecret.PadRight(32, '!'));
    }

    public string GenerateToken(AppUser user, TimeSpan? lifetime = null)
    {
        var validLifetime = lifetime ?? TimeSpan.FromDays(7);
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(validLifetime).ToUnixTimeSeconds();
        var iat = now.ToUnixTimeSeconds();

        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object>
        {
            ["iss"] = _issuer,
            ["aud"] = _audience,
            ["sub"] = user.Id,
            ["name"] = user.Username,
            ["email"] = user.Email,
            ["role"] = user.Role,
            ["shops"] = user.AssignedShopIds,
            ["quota"] = user.MonthlyAiTokenQuota,
            ["used"] = user.UsedAiTokens,
            ["iat"] = iat,
            ["exp"] = exp
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var unsignedToken = $"{headerBase64}.{payloadBase64}";

        using var hmac = new HMACSHA256(_key);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken));
        var signatureBase64 = Base64UrlEncode(signatureBytes);

        return $"{unsignedToken}.{signatureBase64}";
    }

    public (bool IsValid, ClaimsPrincipal? Principal, string? UserId, string? Role, string? Username) ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, null, null, null, null);

        var parts = token.Split('.');
        if (parts.Length != 3) return (false, null, null, null, null);

        var unsignedToken = $"{parts[0]}.{parts[1]}";
        using var hmac = new HMACSHA256(_key);
        var expectedSignatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken));
        var expectedSignature = Base64UrlEncode(expectedSignatureBytes);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(parts[2]),
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return (false, null, null, null, null);
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("exp", out var expElement))
            {
                var expSeconds = expElement.GetInt64();
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expSeconds)
                {
                    return (false, null, null, null, null); // Expired
                }
            }

            var userId = root.GetProperty("sub").GetString() ?? "";
            var username = root.GetProperty("name").GetString() ?? "";
            var role = root.GetProperty("role").GetString() ?? UserRoles.StoreOwner;
            var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() ?? "" : "";

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Name, username),
                new(ClaimTypes.Role, role),
                new(ClaimTypes.Email, email)
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            return (true, principal, userId, role, username);
        }
        catch
        {
            return (false, null, null, null, null);
        }
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }
}
