namespace SimilarProductsWinForms.Services;

using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using SimilarProductsWinForms.Models;

public static class AiBalanceCheckerService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// DeepSeek API resmi bakiye uç noktasını çağırır: GET https://api.deepseek.com/user/balance
    /// </summary>
    public static async Task<AiProviderBalanceInfo> CheckDeepSeekBalanceAsync(string apiKey, CancellationToken ct = default)
    {
        var info = new AiProviderBalanceInfo { Provider = "DeepSeek", CheckedAt = DateTimeOffset.Now };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            info.StatusMessage = "API Anahtarı eksik veya tanımlanmamış.";
            return info;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/user/balance");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var resp = await HttpClient.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                info.StatusMessage = $"Hata (HTTP {(int)resp.StatusCode}): {resp.ReasonPhrase}";
                return info;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            bool isAvailable = root.TryGetProperty("is_available", out var availProp) && availProp.GetBoolean();
            info.IsAvailable = isAvailable;

            if (root.TryGetProperty("balance_infos", out var infosArr) && infosArr.GetArrayLength() > 0)
            {
                var first = infosArr[0];
                if (first.TryGetProperty("currency", out var currProp))
                    info.Currency = currProp.GetString() ?? "USD";

                if (first.TryGetProperty("total_balance", out var totProp))
                    info.TotalBalanceUsd = ParseDecimal(totProp.GetString());

                if (first.TryGetProperty("granted_balance", out var grProp))
                    info.GrantedBalanceUsd = ParseDecimal(grProp.GetString());

                if (first.TryGetProperty("topped_up_balance", out var topProp))
                    info.ToppedUpBalanceUsd = ParseDecimal(topProp.GetString());
            }

            info.StatusMessage = isAvailable
                ? $"✅ Aktif Bakiye: ${info.TotalBalanceUsd:N2} USD (~{info.TotalBalanceTry():N0} ₺)"
                : "⚠️ Bakiye Yetersiz veya Hesap Askıda";

            return info;
        }
        catch (Exception ex)
        {
            info.StatusMessage = $"Bağlantı Hatası: {ex.Message}";
            return info;
        }
    }

    /// <summary>
    /// OpenAI API bağlantı ve durum kontrolü
    /// </summary>
    public static async Task<AiProviderBalanceInfo> CheckOpenAiStatusAsync(string apiKey, CancellationToken ct = default)
    {
        var info = new AiProviderBalanceInfo { Provider = "OpenAI", CheckedAt = DateTimeOffset.Now };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            info.StatusMessage = "API Anahtarı eksik veya tanımlanmamış.";
            return info;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var resp = await HttpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                info.IsAvailable = true;
                info.StatusMessage = "✅ OpenAI API Bağlantısı Aktif (Hazır)";
            }
            else
            {
                info.StatusMessage = resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "❌ Geçersiz API Anahtarı"
                    : $"⚠️ Yanıt Kodu: {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            info.StatusMessage = $"Bağlantı Hatası: {ex.Message}";
        }

        return info;
    }

    /// <summary>
    /// Google Gemini kota ve sağlık durumu kontrolü
    /// </summary>
    public static async Task<AiProviderBalanceInfo> CheckGeminiStatusAsync(string apiKey, CancellationToken ct = default)
    {
        var info = new AiProviderBalanceInfo { Provider = "Google Gemini", CheckedAt = DateTimeOffset.Now };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            info.StatusMessage = "API Anahtarı eksik veya tanımlanmamış.";
            return info;
        }

        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey.Trim())}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);

            using var resp = await HttpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                info.IsAvailable = true;
                info.StatusMessage = "✅ Google Gemini API Bağlantısı Aktif";
            }
            else
            {
                info.StatusMessage = resp.StatusCode == (System.Net.HttpStatusCode)429
                    ? "🚨 Günlük Kota Aşıldı (HTTP 429)"
                    : $"⚠️ Yanıt Kodu: {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            info.StatusMessage = $"Bağlantı Hatası: {ex.Message}";
        }

        return info;
    }

    private static decimal ParseDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0m;
        return decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }
}
