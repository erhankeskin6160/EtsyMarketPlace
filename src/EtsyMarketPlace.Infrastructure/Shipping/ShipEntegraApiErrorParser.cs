namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Text.Json;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// ShipEntegra hata yanıtı çözümleyicisi. İş kuralı redleri 401/403 durum koduyla da
/// gelebildiği için (canlı örnek: ERR.28050.1008, GB gönderisinde DDP reddi, 29.09.2026)
/// yetki hatasından ayırt eder: {status:"fail", data:[{message:"ERR...", description:"..."}]}.
/// </summary>
public static class ShipEntegraApiErrorParser
{
    /// <summary>Gövde bir iş kuralı reddi ise istisna nesnesini döndürür; aksi halde null.</summary>
    public static ShipEntegraBusinessException? TryParse(string? responseBody, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("status", out JsonElement statusElem) ||
                statusElem.ValueKind != JsonValueKind.String ||
                !string.Equals(statusElem.GetString(), "fail", StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("data", out JsonElement dataElem) ||
                dataElem.ValueKind != JsonValueKind.Array ||
                dataElem.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = dataElem[0];
            if (first.ValueKind != JsonValueKind.Object ||
                !first.TryGetProperty("message", out JsonElement messageElem) ||
                messageElem.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string code = messageElem.GetString() ?? string.Empty;
            if (!code.StartsWith("ERR.", StringComparison.Ordinal))
            {
                return null;
            }

            string description =
                first.TryGetProperty("description", out JsonElement descElem) &&
                descElem.ValueKind == JsonValueKind.String
                    ? descElem.GetString() ?? code
                    : code;

            return new ShipEntegraBusinessException(code, description, statusCode);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
