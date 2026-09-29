namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;

/// <summary>
/// ShipEntegra etiket servisinin specialService kodunu çözümler. Eşleme, panel
/// bundle'ındaki servis haritasından (carrier ve createShipEntegraLabel girdileri,
/// 29.09.2026) çıkarılmıştır. Bilinmeyen servislerde null döner; böylece yanlış
/// ürün için etiket satın alınmaz.
/// </summary>
public static class ShipEntegraLabelServiceCodes
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["express"] = "shipentegra-express",
        ["amerika-eko-plus"] = "shipentegra-amerika-eko-plus",
        ["almanya-eko-plus"] = "shipentegra-almanya-eko-plus",
        ["avrupa-eko-plus"] = "shipentegra-avrupa-eko-plus",
        ["avustralya-eko-plus"] = "shipentegra-avustralya-eko-plus",
        ["fransa-eko-plus"] = "shipentegra-fransa-eko-plus",
        ["global-eko-plus"] = "shipentegra-global-eko-plus",
        ["ingiltere-eko-plus"] = "shipentegra-ingiltere-eko-plus",
        ["eko-plus"] = "shipentegra-eko-plus",
        ["ups-ek-servis"] = "shipentegra-ups-ek-servis",
        ["ups-ekspress"] = "shipentegra-ups-ekspress",
        ["fedex"] = "shipentegra-fedex",
        ["worldwide-standard"] = "shipentegra-worldwide-standard",
        ["worldwide-standard-takipsiz"] = "shipentegra-worldwide-standard-takipsiz",
        ["takipsiz"] = "shipentegra-takipsiz",
        ["eco"] = "shipentegra-eco",
        ["fedex-amerika-standard"] = "shipentegra-fedex-amerika-standard",
        ["peak-express"] = "shipentegra-peak-express",
        ["usps"] = "shipentegra-usps",
        ["international-eco"] = "shipentegra-international-eco",
        ["international-express"] = "shipentegra-international-express",
        ["widect"] = "shipentegra-widect",
        ["ecoflex"] = "shipentegra-ecoflex",
        ["ptt"] = "shipentegra-ptt",
        ["dhl"] = "shipentegra-dhl",
        ["tnt"] = "shipentegra-tnt",
        ["ups"] = "shipentegra-ups",
        // Yedek fiyat listesindeki ad; panelde ayni urun shipentegra-express olarak etiketlenir.
        ["shipentegra-smart-express"] = "shipentegra-express",
    };

    /// <summary>specialService kodunu döndürür; bilinmeyen servis için null döner.</summary>
    public static string? Resolve(string? serviceCode, string? displayName)
    {
        foreach (string? candidate in new[] { serviceCode, displayName })
        {
            string key = Slug(candidate);
            if (key.Length == 0)
            {
                continue;
            }

            if (Known.TryGetValue(key, out string? mapped))
            {
                return mapped;
            }

            if (key.StartsWith("shipentegra-", StringComparison.Ordinal) && Known.ContainsValue(key))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>
    /// Panel sözleşmesi: serviceType 1 = Express, 2 = Eko
    /// (panel bundle: serviceType: "express"===shipping_service?1:2).
    /// </summary>
    public static int ResolveServiceType(string? serviceCode)
    {
        if (string.IsNullOrWhiteSpace(serviceCode))
        {
            return 1;
        }

        string code = serviceCode.Trim().ToLowerInvariant();
        bool isEco = code.Contains("eko") || code.Contains("eco");
        return isEco ? 2 : 1;
    }

    private static string Slug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (char raw in value.Trim())
        {
            char ch = raw switch
            {
                'ı' or 'I' or 'İ' or 'i' => 'i',
                'ş' or 'Ş' => 's',
                'ğ' or 'Ğ' => 'g',
                'ü' or 'Ü' => 'u',
                'ö' or 'Ö' => 'o',
                'ç' or 'Ç' => 'c',
                _ => char.ToLowerInvariant(raw)
            };

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else if (ch is ' ' or '-' or '_' or '.' or '/')
            {
                builder.Append('-');
            }
        }

        string slug = builder.ToString();
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Trim('-');
    }
}
