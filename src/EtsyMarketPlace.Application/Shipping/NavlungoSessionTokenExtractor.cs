namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Text.RegularExpressions;

/// <summary>
/// Navlungo oturum kimlik jetonlarını (id_token vb.) serbest metinden ve cookie başlıklarından çıkarır.
/// Gömülü oturum formu ile panel trafiğini tarayan katmanlar bu saf yardımcıyı kullanır.
/// </summary>
public static class NavlungoSessionTokenExtractor
{
    private static readonly Regex IdTokenKeyRegex =
        new("\"id_?token\"\\s*:\\s*\"([^\"\\\\]{20,})\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccessTokenKeyRegex =
        new("\"access_?token\"\\s*:\\s*\"([^\"\\\\]{20,})\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GenericTokenKeyRegex =
        new("\"token\"\\s*:\\s*\"(eyJ[^\"\\\\]{20,})\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JwtRegex =
        new("eyJ[A-Za-z0-9_\\-]{10,}\\.[A-Za-z0-9_\\-]{10,}\\.[A-Za-z0-9_\\-]{4,}", RegexOptions.Compiled);

    private static readonly Regex CookieIdTokenRegex =
        new("(?:^|[;\\s])id_token=([^;\\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Metin içinde (JSON gövde, depo dökümü vb.) Navlungo oturum jetonu arar; bulamazsa null döner.</summary>
    public static string? TryExtractIdToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string normalized = text.Replace("\\\"", "\"");

        var idToken = IdTokenKeyRegex.Match(normalized);
        if (idToken.Success)
        {
            return idToken.Groups[1].Value.Trim();
        }

        var accessToken = AccessTokenKeyRegex.Match(normalized);
        if (accessToken.Success)
        {
            return accessToken.Groups[1].Value.Trim();
        }

        var generic = GenericTokenKeyRegex.Match(normalized);
        if (generic.Success)
        {
            return generic.Groups[1].Value.Trim();
        }

        var jwt = JwtRegex.Match(normalized);
        return jwt.Success ? jwt.Value.Trim() : null;
    }

    /// <summary>Cookie başlığından id_token değerini ayıklar (yoksa null).</summary>
    public static string? TryExtractFromCookieHeader(string? cookieHeader)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return null;
        }

        var match = CookieIdTokenRegex.Match(cookieHeader);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
