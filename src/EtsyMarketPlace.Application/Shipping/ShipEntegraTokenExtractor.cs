namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Text.RegularExpressions;

/// <summary>
/// ShipEntegra tokenlarını (v4.public.* veya JWT) serbest metinden çıkarır.
/// Panel trafiğini ve yerel depolamayı tarayıp tokeni yakalayan katmanlar bu saf yardımcıyı kullanır.
/// </summary>
public static class ShipEntegraTokenExtractor
{
    private static readonly Regex AccessTokenRegex =
        new("\"accessToken\"\\s*:\\s*\"([^\"]{20,})\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RefreshTokenRegex =
        new("\"refreshToken\"\\s*:\\s*\"([^\"]{20,})\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JwtRegex =
        new("ey[A-Za-z0-9_-]{10,}\\.ey[A-Za-z0-9_-]{10,}\\.[A-Za-z0-9_-]+", RegexOptions.Compiled);

    private static readonly Regex V4TokenRegex =
        new("v4\\.[A-Za-z0-9_.\\-]{40,}", RegexOptions.Compiled);

    /// <summary>Ham "Bearer x" ifadesinden veya düz metinden geçerli görünen tokeni döndürür.</summary>
    public static string? NormalizeToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string t = raw.Trim().Trim('"');
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            t = t.Substring(7).Trim();
        }

        return LooksLikeToken(t) ? t : null;
    }

    /// <summary>Metnin token biçiminde olup olmadığını kaba kuralla doğrular.</summary>
    public static bool LooksLikeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string v = value.Trim();
        if (v.Length < 40 || v.Length > 8192) return false;
        if (v.Contains(' ')) return false;

        if (V4TokenRegex.IsMatch(v) || JwtRegex.IsMatch(v)) return true;
        if (v.StartsWith("v4.", StringComparison.OrdinalIgnoreCase)) return true;

        // Diğer opak biçimler: uzun ve yalnızca token karakterleri içeriyorsa kabul et.
        return v.Length >= 100 && Regex.IsMatch(v, "^[A-Za-z0-9_.\\-]+$");
    }

    /// <summary>Yanıt gövdesi/depolama dökümü içinden accessToken + refreshToken çıkarır.</summary>
    public static (string? AccessToken, string? RefreshToken) ExtractFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);

        string? access = null;
        string? refresh = null;

        var am = AccessTokenRegex.Match(text);
        if (am.Success) access = am.Groups[1].Value.Trim();

        var rm = RefreshTokenRegex.Match(text);
        if (rm.Success) refresh = rm.Groups[1].Value.Trim();

        if (access == null)
        {
            var vm = V4TokenRegex.Match(text);
            if (vm.Success) access = vm.Value.Trim();
        }

        if (access == null)
        {
            var jm = JwtRegex.Match(text);
            if (jm.Success) access = jm.Value;
        }

        return (access, refresh);
    }

    /// <summary>İstek başlığı değerinden token çıkarır ("Bearer x" ya da gömülü v4/JWT).</summary>
    public static string? ExtractTokenFromAny(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var v4 = V4TokenRegex.Match(value);
        if (v4.Success) return v4.Value.Trim();

        var jwt = JwtRegex.Match(value);
        if (jwt.Success) return jwt.Value;

        return NormalizeToken(value);
    }
}
