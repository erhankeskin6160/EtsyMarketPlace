namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Text.RegularExpressions;

/// <summary>
/// DevTools'tan kopyalanan cURL, HTTP Request Headers veya ham çerez dizesini ayıklayıp temizleyen yardımcı sınıf.
/// </summary>
public static class NavlungoCookieSanitizer
{
    public static string Sanitize(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput)) return string.Empty;

        string input = rawInput.Trim();

        // 1. cURL formatı: -H "cookie: ..." veya -H 'cookie: ...'
        var curlMatch = Regex.Match(input, @"(?:-H|--header)\s+['""]cookie:\s*([^'""]+)['""]", RegexOptions.IgnoreCase);
        if (curlMatch.Success)
        {
            return CleanCookieValue(curlMatch.Groups[1].Value);
        }

        var curlSimpleMatch = Regex.Match(input, @"(?:-b|--cookie)\s+['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
        if (curlSimpleMatch.Success)
        {
            return CleanCookieValue(curlSimpleMatch.Groups[1].Value);
        }

        // 2. HTTP Raw Headers formatı (DevTools'tan tüm istek başlıkları kopyalandığında)
        var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
            {
                return CleanCookieValue(trimmedLine[7..].Trim());
            }
        }

        // 3. Tek satır "Cookie: ..." formatı
        if (input.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
        {
            return CleanCookieValue(input[7..].Trim());
        }

        // 4. Genel temizlik (satır sonlarını tek boşluğa indir)
        return CleanCookieValue(input);
    }

    private static string CleanCookieValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string cleaned = Regex.Replace(value, @"[\r\n\t]+", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s*;\s*", "; ");
        return cleaned.Trim('\'', '\"', ' ');
    }
}
