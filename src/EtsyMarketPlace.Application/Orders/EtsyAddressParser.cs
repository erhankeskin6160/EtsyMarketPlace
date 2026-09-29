namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Etsy API sipariş yanıtlarındaki alıcı ve teslimat adreslerini
/// farklı JSON şemaları ve formatted_address fallback'i üzerinden ayrıştıran yardımcı sınıf.
/// </summary>
public static class EtsyAddressParser
{
    public sealed record ParsedAddress(
        string BuyerName,
        string BuyerEmail,
        string Phone,
        string StreetAddress,
        string SecondAddress,
        string City,
        string State,
        string PostalCode,
        string CountryCode,
        string FormattedAddress);

    public static ParsedAddress Parse(JsonElement receipt)
    {
        string name = GetFirst(receipt, "name", "buyer_name", "recipient_name", "shipping_name");
        string email = GetFirst(receipt, "buyer_email", "email", "payment_email");
        string phone = GetFirst(receipt, "buyer_phone", "phone", "phone_number", "receiver_phone");
        string firstLine = GetFirst(receipt, "first_line", "street_address", "address_line1", "address1");
        string secondLine = GetFirst(receipt, "second_line", "address_line2", "address2");
        string city = GetFirst(receipt, "city", "town", "city_name");
        string state = GetFirst(receipt, "state", "province", "region", "state_code");
        string zip = GetFirst(receipt, "zip", "postal_code", "postcode", "zip_code");
        string country = GetFirst(receipt, "country_iso", "country_code", "country");
        string formatted = GetFirst(receipt, "formatted_address");

        // 1. Eğer üst düzeyde eksik varsa iç içe nesneleri kontrol et (shipping_address, destination_address vb.)
        string[] nestedNames = { "shipping_address", "destination_address", "buyer_address", "recipient" };
        foreach (var nestedProp in nestedNames)
        {
            if (receipt.TryGetProperty(nestedProp, out var sub) && sub.ValueKind == JsonValueKind.Object)
            {
                if (string.IsNullOrWhiteSpace(name)) name = GetFirst(sub, "name", "buyer_name", "recipient_name");
                if (string.IsNullOrWhiteSpace(email)) email = GetFirst(sub, "buyer_email", "email");
                if (string.IsNullOrWhiteSpace(phone)) phone = GetFirst(sub, "buyer_phone", "phone", "phone_number");
                if (string.IsNullOrWhiteSpace(firstLine)) firstLine = GetFirst(sub, "first_line", "street_address", "address_line1", "address1");
                if (string.IsNullOrWhiteSpace(secondLine)) secondLine = GetFirst(sub, "second_line", "address_line2", "address2");
                if (string.IsNullOrWhiteSpace(city)) city = GetFirst(sub, "city", "town");
                if (string.IsNullOrWhiteSpace(state)) state = GetFirst(sub, "state", "province", "region");
                if (string.IsNullOrWhiteSpace(zip)) zip = GetFirst(sub, "zip", "postal_code", "postcode");
                if (string.IsNullOrWhiteSpace(country)) country = GetFirst(sub, "country_iso", "country_code", "country");
                if (string.IsNullOrWhiteSpace(formatted)) formatted = GetFirst(sub, "formatted_address");
            }
        }

        // 2. Eğer firstLine veya city hala boşsa ve formatted_address varsa, formatted_address'ten çıkar
        if ((string.IsNullOrWhiteSpace(firstLine) || string.IsNullOrWhiteSpace(city)) && !string.IsNullOrWhiteSpace(formatted))
        {
            var fallback = ParseFromFormattedAddress(formatted);
            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(fallback.BuyerName)) name = fallback.BuyerName;
            if (string.IsNullOrWhiteSpace(firstLine) && !string.IsNullOrWhiteSpace(fallback.StreetAddress)) firstLine = fallback.StreetAddress;
            if (string.IsNullOrWhiteSpace(secondLine) && !string.IsNullOrWhiteSpace(fallback.SecondAddress)) secondLine = fallback.SecondAddress;
            if (string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(fallback.City)) city = fallback.City;
            if (string.IsNullOrWhiteSpace(state) && !string.IsNullOrWhiteSpace(fallback.State)) state = fallback.State;
            if (string.IsNullOrWhiteSpace(zip) && !string.IsNullOrWhiteSpace(fallback.PostalCode)) zip = fallback.PostalCode;
            if (string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(fallback.CountryCode)) country = fallback.CountryCode;
        }

        // 3. ABD / Kanada için eyalet normalizasyonu
        if (UsStateHelper.RequiresState(country) && !string.IsNullOrWhiteSpace(state))
        {
            var (normCode, _) = UsStateHelper.ResolveUsOrCaState(state);
            state = normCode;
        }

        return new ParsedAddress(
            name,
            email,
            phone,
            firstLine,
            secondLine,
            city,
            state,
            zip,
            country,
            formatted);
    }

    /// <summary>
    /// Çok satırlı formatted_address metnini ayrıştırır.
    /// Tipik biçim:
    /// Satır 1: İsim (opsiyonel)
    /// Satır 2: Sokak Adresi 1
    /// Satır 3: Sokak Adresi 2 (opsiyonel)
    /// Satır 4: Şehir, Eyalet PostaKodu
    /// Satır 5: Ülke
    /// </summary>
    public static ParsedAddress ParseFromFormattedAddress(string formatted)
    {
        if (string.IsNullOrWhiteSpace(formatted))
        {
            return new ParsedAddress("", "", "", "", "", "", "", "", "", "");
        }

        var lines = formatted
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
        {
            return new ParsedAddress("", "", "", "", "", "", "", "", "", formatted);
        }

        string name = "";
        string street = "";
        string second = "";
        string city = "";
        string state = "";
        string zip = "";
        string country = "";

        if (lines.Count == 1)
        {
            street = lines[0];
        }
        else if (lines.Count == 2)
        {
            street = lines[0];
            ParseCityStateZipLine(lines[1], ref city, ref state, ref zip);
        }
        else if (lines.Count == 3)
        {
            street = lines[0];
            ParseCityStateZipLine(lines[1], ref city, ref state, ref zip);
            country = lines[2];
        }
        else if (lines.Count == 4)
        {
            name = lines[0];
            street = lines[1];
            ParseCityStateZipLine(lines[2], ref city, ref state, ref zip);
            country = lines[3];
        }
        else // 5 veya daha fazla satır
        {
            name = lines[0];
            street = lines[1];
            second = lines[2];
            ParseCityStateZipLine(lines[lines.Count - 2], ref city, ref state, ref zip);
            country = lines[lines.Count - 1];
        }

        return new ParsedAddress(name, "", "", street, second, city, state, zip, country, formatted);
    }

    private static void ParseCityStateZipLine(string line, ref string city, ref string state, ref string zip)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        // Örnek 1: "Brooklyn, NY 11201" veya "New York, NY 10001"
        var matchUs = Regex.Match(line, @"^([^,]+),\s*([A-Za-z]{2})\s+([0-9A-Za-z\-]+)$");
        if (matchUs.Success)
        {
            city = matchUs.Groups[1].Value.Trim();
            state = matchUs.Groups[2].Value.Trim().ToUpperInvariant();
            zip = matchUs.Groups[3].Value.Trim();
            return;
        }

        // Örnek 2: "Miami FL 33101" (virgülsüz)
        var matchUsNoComma = Regex.Match(line, @"^(.+?)\s+([A-Za-z]{2})\s+([0-9A-Za-z\-]+)$");
        if (matchUsNoComma.Success)
        {
            city = matchUsNoComma.Groups[1].Value.Trim();
            state = matchUsNoComma.Groups[2].Value.Trim().ToUpperInvariant();
            zip = matchUsNoComma.Groups[3].Value.Trim();
            return;
        }

        // Örnek 3: Avrupa formatı "56068 Koblenz"
        var matchEu = Regex.Match(line, @"^([0-9A-Za-z\-]{3,10})\s+(.+)$");
        if (matchEu.Success)
        {
            zip = matchEu.Groups[1].Value.Trim();
            city = matchEu.Groups[2].Value.Trim();
            return;
        }

        // Örnek 4: Virgüle göre basit ayırma
        if (line.Contains(','))
        {
            var parts = line.Split(',');
            city = parts[0].Trim();
            if (parts.Length > 1)
            {
                var remaining = parts[1].Trim().Split(' ');
                if (remaining.Length == 2)
                {
                    state = remaining[0];
                    zip = remaining[1];
                }
                else
                {
                    state = parts[1].Trim();
                }
            }
            return;
        }

        // Fallback
        city = line.Trim();
    }

    private static string GetFirst(JsonElement element, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (element.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.String)
                {
                    string str = val.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(str)) return str.Trim();
                }
                else if (val.ValueKind == JsonValueKind.Number)
                {
                    return val.ToString();
                }
            }
        }
        return "";
    }
}
