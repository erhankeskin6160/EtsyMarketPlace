namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// ShipEntegra panelinin ABD gönderileri için uyguladığı HS/HTS kodu doğrulamasının
/// birebir eşleniği. Panel kodu noktalı biçime çevirip (da() fonksiyonu) kendi gömülü
/// ABD HTS listesiyle karşılaştırır; liste panel bundle'ından çıkarılmıştır
/// (23.457 kod, 29.09.2026 sürümü).
/// </summary>
public static class ShipEntegraHsCodeCatalog
{
    private static readonly Lazy<HashSet<string>> UsCodes = new(LoadCodes);

    /// <summary>Tüm gömülü ABD HTS kodları (noktalı biçim).</summary>
    public static IReadOnlyCollection<string> AllUsCodes => UsCodes.Value;

    /// <summary>
    /// "8504403000" -> "8504.40.30.00" (panel da() ile aynı gruplama: 4-2-2-2-2).
    /// Rakam dışı tüm karakterler temizlenir.
    /// </summary>
    public static string Format(string? hsCode)
    {
        string digits = new string((hsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        int pos = 0;
        foreach (int size in new[] { 4, 2, 2, 2, 2 })
        {
            if (pos >= digits.Length)
            {
                break;
            }

            parts.Add(digits.Substring(pos, Math.Min(size, digits.Length - pos)));
            pos += size;
        }

        return string.Join('.', parts);
    }

    /// <summary>
    /// Panel kuralı: ABD için kod en az 6 haneli olmalı ve biçimlenmiş hâli
    /// gömülü ABD HTS listesinde bulunmalı.
    /// </summary>
    public static bool IsUsCode(string? hsCode)
    {
        string digits = new string((hsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 6)
        {
            return false;
        }

        return UsCodes.Value.Contains(Format(hsCode));
    }

    /// <summary>Geçersiz kod için aynı fasıldan benzer geçerli kodlar önerir.</summary>
    public static string SuggestFor(string? hsCode, int max = 6)
    {
        string formatted = Format(hsCode);
        string? prefix = null;
        if (formatted.Length >= 7)
        {
            string six = formatted.Substring(0, 7);
            if (UsCodes.Value.Any(c => c.StartsWith(six, StringComparison.Ordinal)))
            {
                prefix = six;
            }
        }

        if (prefix == null && formatted.Length >= 4)
        {
            prefix = formatted.Substring(0, 4) + ".";
        }

        if (string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }

        return string.Join(", ", UsCodes.Value
            .Where(c => c.StartsWith(prefix!, StringComparison.Ordinal))
            .OrderBy(c => c, StringComparer.Ordinal)
            .Take(max));
    }

    private static HashSet<string> LoadCodes()
    {
        var assembly = typeof(ShipEntegraHsCodeCatalog).Assembly;
        string? resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("se-us-hs-codes.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            throw new InvalidOperationException("Gömülü ABD HTS listesi (se-us-hs-codes.json) bulunamadı.");
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        var list = JsonSerializer.Deserialize<List<string>>(stream) ?? new List<string>();
        return new HashSet<string>(list, StringComparer.Ordinal);
    }
}
