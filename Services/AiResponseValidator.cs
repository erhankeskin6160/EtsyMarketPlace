namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>
/// AI yanıtlarından JSON çıktısını doğrulayan ve parse eden yardımcı.
/// Geçersiz JSON'da otomatik düzeltme dener, başarısız olursa null döner.
/// </summary>
internal static class AiResponseValidator
{
    /// <summary>
    /// AI yanıtından JSON bloğunu çıkar ve doğrula.
    /// Markdown code fence içindeki JSON'u da algılar.
    /// </summary>
    public static JsonDocument? ExtractAndValidateJson(string aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse)) return null;

        string cleaned = aiResponse.Trim();

        // Markdown ```json ... ``` bloğunu çıkar
        int jsonStart = cleaned.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            int contentStart = cleaned.IndexOf('\n', jsonStart);
            if (contentStart >= 0)
            {
                int jsonEnd = cleaned.IndexOf("```", contentStart + 1);
                if (jsonEnd > contentStart)
                {
                    cleaned = cleaned.Substring(contentStart + 1, jsonEnd - contentStart - 1).Trim();
                }
            }
        }
        else
        {
            // İlk { veya [ bulunana kadar temizle
            int firstBrace = cleaned.IndexOf('{');
            int firstBracket = cleaned.IndexOf('[');
            int start = -1;

            if (firstBrace >= 0 && firstBracket >= 0)
                start = Math.Min(firstBrace, firstBracket);
            else if (firstBrace >= 0)
                start = firstBrace;
            else if (firstBracket >= 0)
                start = firstBracket;

            if (start > 0)
            {
                cleaned = cleaned.Substring(start);
            }

            // Sondaki gereksiz metni temizle
            int lastBrace = cleaned.LastIndexOf('}');
            int lastBracket = cleaned.LastIndexOf(']');
            int end = Math.Max(lastBrace, lastBracket);
            if (end > 0 && end < cleaned.Length - 1)
            {
                cleaned = cleaned.Substring(0, end + 1);
            }
        }

        try
        {
            return JsonDocument.Parse(cleaned);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// JSON dokümanından belirli bir property'yi güvenle çıkar.
    /// </summary>
    public static string GetStringProperty(JsonDocument doc, string propertyName, string defaultValue = "")
    {
        try
        {
            if (doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString() ?? defaultValue;
            }
        }
        catch { }
        return defaultValue;
    }

    /// <summary>
    /// JSON dokümanından int property çıkar.
    /// </summary>
    public static int GetIntProperty(JsonDocument doc, string propertyName, int defaultValue = 0)
    {
        try
        {
            if (doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetInt32();
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out int val))
                    return val;
            }
        }
        catch { }
        return defaultValue;
    }

    /// <summary>
    /// JSON dokümanından string array çıkar.
    /// </summary>
    public static List<string> GetStringArrayProperty(JsonDocument doc, string propertyName)
    {
        var result = new List<string>();
        try
        {
            if (doc.RootElement.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) result.Add(s);
                }
            }
        }
        catch { }
        return result;
    }
}
