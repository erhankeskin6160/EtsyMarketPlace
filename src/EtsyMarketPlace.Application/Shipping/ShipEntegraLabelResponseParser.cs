namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Text.Json;

/// <summary>
/// ShipEntegra etiket yanıtı çözümleyicisi. Panel sözleşmesi (29.09.2026 canlı trafik):
/// etiket POST'u JSON döner; gerçek PDF, data.label alanındaki herkese açık URL'dedir
/// (files.shipentegra.com). Doğrudan PDF baytı yanıtı da desteklenir.
/// </summary>
public static class ShipEntegraLabelResponseParser
{
    /// <summary>Yanıt JSON ise data.label (http/https) adresini döner; aksi halde null.</summary>
    public static string? TryExtractLabelFileUrl(byte[]? responseBytes)
    {
        if (responseBytes == null || responseBytes.Length == 0 || responseBytes[0] == 0x25)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBytes);
            if (!doc.RootElement.TryGetProperty("data", out JsonElement data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("label", out JsonElement label) ||
                label.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? url = label.GetString();
            return !string.IsNullOrWhiteSpace(url) &&
                   (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                ? url
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Bayt dizisi PDF imzasıyla (%PDF) başlıyor mu?</summary>
    public static bool IsPdf(byte[]? bytes) =>
        bytes is { Length: > 4 } &&
        bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
}
