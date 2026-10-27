namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Text;
using System.Text.Json;

/// <summary>
/// JWT (JSON Web Token) ve Paseto (v4.public) geçerlilik ve süre sonu (expiration) denetleyicisi.
/// Harici kütüphanelere ihtiyaç duymadan token payload'ındaki "exp" alanını çözer.
/// </summary>
public static class JwtTokenInspector
{
    /// <summary>
    /// Verilen JWT veya Paseto token'ın süresinin dolup dolmadığını kontrol eder.
    /// </summary>
    /// <param name="token">JWT/Paseto Bearer token (başında 'Bearer ' olabilir veya olmayabilir)</param>
    /// <param name="leewaySeconds">Erken yenileme için tolerans süresi (varsayılan: 60 saniye)</param>
    /// <returns>Token boş, geçersiz veya süresi dolmuşsa true; geçerliyse false döner.</returns>
    public static bool IsExpired(string? token, int leewaySeconds = 60)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;

        var expiry = GetExpirationUtc(token);
        if (expiry == null)
        {
            return true;
        }

        long currentSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long expSeconds = new DateTimeOffset(expiry.Value).ToUnixTimeSeconds();
        return currentSeconds >= (expSeconds - leewaySeconds);
    }

    /// <summary>
    /// Birim testler ve simülasyonlar için sentetik geçerli bir JWT üretir.
    /// </summary>
    public static string CreateSyntheticToken(TimeSpan? lifetime = null, string sub = "test_user")
    {
        var duration = lifetime ?? TimeSpan.FromHours(4);
        long exp = DateTimeOffset.UtcNow.Add(duration).ToUnixTimeSeconds();
        string headerJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        string payloadJson = $"{{\"sub\":\"{sub}\",\"exp\":{exp}}}";
        string b64Header = Convert.ToBase64String(Encoding.UTF8.GetBytes(headerJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string b64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{b64Header}.{b64Payload}.synthetic_test_signature";
    }

    /// <summary>
    /// Birim testler için sentetik geçerli bir Paseto v4 token üretir.
    /// </summary>
    public static string CreateSyntheticPasetoToken(TimeSpan? lifetime = null, string sub = "test_user")
    {
        var duration = lifetime ?? TimeSpan.FromHours(4);
        string expIso = DateTimeOffset.UtcNow.Add(duration).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        string payloadJson = $"{{\"sub\":\"{sub}\",\"exp\":\"{expIso}\"}}";
        string b64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"v4.public.{b64Payload}.synthetic_paseto_signature";
    }

    /// <summary>
    /// Token'ın son geçerlilik tarihini (UTC) döner. exp bulunamazsa veya format tanımsızsa null döner.
    /// Standart JWT (alg.payload.sig) ve Paseto v4 (v4.public.payload.sig) formatlarını destekler.
    /// </summary>
    public static DateTime? GetExpirationUtc(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        string clean = token.Trim();
        if (clean.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[7..].Trim();
        }

        try
        {
            string? payloadJson = ExtractPayloadJson(clean);
            if (string.IsNullOrWhiteSpace(payloadJson)) return null;

            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("exp", out var expProp))
            {
                if (expProp.ValueKind == JsonValueKind.Number && expProp.TryGetInt64(out long expSeconds))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                }

                if (expProp.ValueKind == JsonValueKind.String)
                {
                    string expStr = expProp.GetString() ?? string.Empty;
                    if (long.TryParse(expStr, out long parsedSeconds))
                    {
                        return DateTimeOffset.FromUnixTimeSeconds(parsedSeconds).UtcDateTime;
                    }

                    if (DateTimeOffset.TryParse(expStr, out var dto))
                    {
                        return dto.UtcDateTime;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static string? ExtractPayloadJson(string cleanToken)
    {
        var parts = cleanToken.Split('.');

        // 1) Paseto v4 public: v4.public.<payload>.<sig>
        if (cleanToken.StartsWith("v4.public.", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
        {
            return DecodeBase64Url(parts[2]);
        }

        // 2) Diğer olası Paseto türleri (v2, v3, v4): <version>.<purpose>.<payload>.<sig>
        if (parts.Length >= 3 && (cleanToken.StartsWith("v4.", StringComparison.OrdinalIgnoreCase) ||
                                  cleanToken.StartsWith("v2.", StringComparison.OrdinalIgnoreCase)))
        {
            return DecodeBase64Url(parts[2]);
        }

        // 3) Standart JWT: <header>.<payload>.<sig>
        if (parts.Length >= 2 && cleanToken.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase))
        {
            return DecodeBase64Url(parts[1]);
        }

        // 4) Genel 3 parçalı token fallback
        if (parts.Length >= 3)
        {
            try
            {
                return DecodeBase64Url(parts[1]);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string DecodeBase64Url(string input)
    {
        string output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        byte[] bytes = Convert.FromBase64String(output);
        return Encoding.UTF8.GetString(bytes);
    }
}
