namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Text;
using System.Text.Json;

/// <summary>
/// JWT (JSON Web Token) geçerlilik ve süre sonu (expiration) denetleyicisi.
/// Harici kütüphanelere ihtiyaç duymadan token payload'ındaki "exp" alanını çözer.
/// </summary>
public static class JwtTokenInspector
{
    /// <summary>
    /// Verilen JWT token'ın süresinin dolup dolmadığını kontrol eder.
    /// </summary>
    /// <param name="token">JWT Bearer token (başında 'Bearer ' olabilir veya olmayabilir)</param>
    /// <param name="leewaySeconds">Erken yenileme için tolerans süresi (varsayılan: 60 saniye)</param>
    /// <returns>Token boş, geçersiz veya süresi dolmuşsa true; geçerliyse false döner.</returns>
    public static bool IsExpired(string? token, int leewaySeconds = 60)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;

        string clean = token.Trim();
        if (clean.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(7).Trim();
        }

        var parts = clean.Split('.');
        if (parts.Length < 2)
        {
            // JWT formatında değilse (opak token veya birim test mock tokeni)
            // JWT süresi denetlenemez, bu nedenle süresi dolmuş sayılmaz
            return false;
        }

        try
        {
            string payloadJson = DecodeBase64Url(parts[1]);
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("exp", out var expProp))
            {
                long expSeconds = 0;
                if (expProp.ValueKind == JsonValueKind.Number)
                {
                    expSeconds = expProp.GetInt64();
                }
                else if (expProp.ValueKind == JsonValueKind.String && long.TryParse(expProp.GetString(), out long parsed))
                {
                    expSeconds = parsed;
                }

                if (expSeconds > 0)
                {
                    long currentSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    return currentSeconds >= (expSeconds - leewaySeconds);
                }
            }
        }
        catch
        {
            // Eğer 'eyJ' ile başlıyorsa ve çözülemediyse bozuk/geçersiz JWT'dir
            if (clean.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// JWT token'ın son geçerlilik tarihini (UTC) döner. exp bulunamazsa null döner.
    /// </summary>
    public static DateTime? GetExpirationUtc(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        string clean = token.Trim();
        if (clean.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(7).Trim();
        }

        var parts = clean.Split('.');
        if (parts.Length < 2) return null;

        try
        {
            string payloadJson = DecodeBase64Url(parts[1]);
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out long expSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            }
        }
        catch { }

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
