namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Etsy Satıcı Panelinden indirilen Sipariş CSV (EtsySoldOrders*.csv) dosyalarını
/// ayrıştırarak alıcı teslimat adreslerini çıkaran servis.
/// Tırnak içi virgülleri, farklı sütun başlıklarını ve eyalet kodlarını akıllıca yönetir.
/// </summary>
public static class EtsyOrderCsvParser
{
    public static List<OrderAddressRecord> ParseCsv(string csvContent, string source = "EtsyCsv")
    {
        if (string.IsNullOrWhiteSpace(csvContent)) return new List<OrderAddressRecord>();

        using var reader = new StringReader(csvContent);
        return Parse(reader, source);
    }

    public static List<OrderAddressRecord> ParseFile(string filePath, string source = "EtsyCsv")
    {
        if (!File.Exists(filePath)) return new List<OrderAddressRecord>();

        // UTF-8 veya varsayılan ANSI kodlamasını destekle
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Parse(reader, source);
    }

    private static List<OrderAddressRecord> Parse(TextReader reader, string source)
    {
        var records = new List<OrderAddressRecord>();
        string? headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine)) return records;

        var headers = ParseCsvLine(headerLine);
        var headerMap = BuildHeaderIndexMap(headers);

        if (!headerMap.ContainsKey("order_id"))
        {
            // Order ID kolonu bulunamadı, geçerli bir Etsy sipariş CSV'si değil
            return records;
        }

        int idxOrderId = headerMap["order_id"];
        int idxName = headerMap.GetValueOrDefault("name", -1);
        int idxAddress1 = headerMap.GetValueOrDefault("address1", -1);
        int idxAddress2 = headerMap.GetValueOrDefault("address2", -1);
        int idxCity = headerMap.GetValueOrDefault("city", -1);
        int idxState = headerMap.GetValueOrDefault("state", -1);
        int idxZip = headerMap.GetValueOrDefault("zip", -1);
        int idxCountry = headerMap.GetValueOrDefault("country", -1);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count <= idxOrderId) continue;

            string rawOrderId = cols[idxOrderId].Trim().TrimStart('#');
            if (!long.TryParse(rawOrderId, out long receiptId) || receiptId <= 0) continue;

            string name = idxName >= 0 && idxName < cols.Count ? cols[idxName].Trim() : "";
            string street1 = idxAddress1 >= 0 && idxAddress1 < cols.Count ? cols[idxAddress1].Trim() : "";
            string street2 = idxAddress2 >= 0 && idxAddress2 < cols.Count ? cols[idxAddress2].Trim() : "";
            string city = idxCity >= 0 && idxCity < cols.Count ? cols[idxCity].Trim() : "";
            string state = idxState >= 0 && idxState < cols.Count ? cols[idxState].Trim() : "";
            string zip = idxZip >= 0 && idxZip < cols.Count ? cols[idxZip].Trim() : "";
            string country = idxCountry >= 0 && idxCountry < cols.Count ? cols[idxCountry].Trim() : "US";

            // En az sokak veya şehir bulunmalı
            if (string.IsNullOrWhiteSpace(street1) && string.IsNullOrWhiteSpace(city)) continue;

            // Ülke kodu normalize et (Örn: "United States" -> "US")
            string countryCode = NormalizeCountryCode(country);

            // Eyalet normalize et (Örn: "California" -> "CA")
            if (UsStateHelper.RequiresState(countryCode) && !string.IsNullOrWhiteSpace(state))
            {
                var (normState, _) = UsStateHelper.ResolveUsOrCaState(state);
                state = normState;
            }

            records.Add(new OrderAddressRecord(
                receiptId,
                name,
                BuyerEmail: "",
                Phone: "",
                street1,
                street2,
                city,
                state,
                zip,
                countryCode,
                country,
                source,
                DateTime.UtcNow));
        }

        return records;
    }

    private static Dictionary<string, int> BuildHeaderIndexMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headers.Count; i++)
        {
            string h = NormalizeHeaderName(headers[i]);

            if (h.Contains("orderid") || h.Equals("saleid") || h.Equals("receiptid") || h.Equals("ordernumber"))
            {
                map.TryAdd("order_id", i);
            }
            else if (h.Contains("shipaddress1") || h.Equals("street1") || h.Equals("address1") || h.Equals("street") || h.Equals("address"))
            {
                map.TryAdd("address1", i);
            }
            else if (h.Contains("shipaddress2") || h.Equals("street2") || h.Equals("address2"))
            {
                map.TryAdd("address2", i);
            }
            else if (h.Contains("shipcity") || h.Equals("city"))
            {
                map.TryAdd("city", i);
            }
            else if (h.Contains("shipstate") || h.Equals("state") || h.Equals("province"))
            {
                map.TryAdd("state", i);
            }
            else if (h.Contains("shipzip") || h.Equals("zipcode") || h.Equals("zip") || h.Contains("postalcode") || h.Equals("postcode"))
            {
                map.TryAdd("zip", i);
            }
            else if (h.Contains("shipcountry") || h.Equals("country"))
            {
                map.TryAdd("country", i);
            }
            else if (h.Contains("fullname") || h.Contains("buyername") || h.Contains("recipientname") || h.Equals("shipname") || h.Equals("name"))
            {
                map.TryAdd("name", i);
            }
        }

        return map;
    }

    private static string NormalizeHeaderName(string raw)
    {
        return new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string NormalizeCountryCode(string country)
    {
        if (string.IsNullOrWhiteSpace(country)) return "US";
        string trimmed = country.Trim();
        if (trimmed.Length == 2) return trimmed.ToUpperInvariant();

        string lower = trimmed.ToLowerInvariant();
        if (lower.Contains("united states") || lower == "usa" || lower == "us") return "US";
        if (lower.Contains("canada")) return "CA";
        if (lower.Contains("united kingdom") || lower == "uk" || lower.Contains("great britain")) return "GB";
        if (lower.Contains("germany") || lower == "deutschland") return "DE";
        if (lower.Contains("france")) return "FR";
        if (lower.Contains("australia")) return "AU";
        if (lower.Contains("italy") || lower == "italia") return "IT";
        if (lower.Contains("spain") || lower == "espana") return "ES";
        if (lower.Contains("netherlands") || lower == "holland") return "NL";
        if (lower.Contains("turkey") || lower.Contains("türkiye")) return "TR";

        return trimmed.Length <= 3 ? trimmed.ToUpperInvariant() : "US";
    }

    public static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line)) return result;

        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString().Trim());
        return result;
    }
}
