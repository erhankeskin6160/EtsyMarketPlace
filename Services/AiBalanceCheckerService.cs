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
    /// DeepSeek API resmi bakiye ve model uç noktalarını çağırır: GET https://api.deepseek.com/user/balance
    /// </summary>
    public static async Task<AiProviderBalanceInfo> CheckDeepSeekBalanceAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiProviderBalanceInfo
            {
                Provider = "DeepSeek",
                CheckedAt = DateTimeOffset.Now,
                StatusMessage = "API Anahtarı eksik veya tanımlanmamış."
            };
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/user/balance");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var resp = await HttpClient.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                return new AiProviderBalanceInfo
                {
                    Provider = "DeepSeek",
                    CheckedAt = DateTimeOffset.Now,
                    MaskedApiKey = AiPriceCalculator.MaskApiKey(apiKey),
                    StatusMessage = $"Hata (HTTP {(int)resp.StatusCode}): {resp.ReasonPhrase}"
                };
            }

            var info = AiPriceCalculator.ParseDeepSeekBalanceJson(body, apiKey);

            // Modelleri doğrula
            try
            {
                using var modelReq = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/models");
                modelReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                using var modelResp = await HttpClient.SendAsync(modelReq, ct);
                if (modelResp.IsSuccessStatusCode)
                {
                    string modelBody = await modelResp.Content.ReadAsStringAsync(ct);
                    info.AvailableModels = AiPriceCalculator.ParseDeepSeekModelsJson(modelBody);
                }
            }
            catch { }

            return info;
        }
        catch (Exception ex)
        {
            return new AiProviderBalanceInfo
            {
                Provider = "DeepSeek",
                CheckedAt = DateTimeOffset.Now,
                MaskedApiKey = AiPriceCalculator.MaskApiKey(apiKey),
                StatusMessage = $"Bağlantı Hatası: {ex.Message}"
            };
        }
    }

    public static string MaskApiKey(string? apiKey) => AiPriceCalculator.MaskApiKey(apiKey);

    public static decimal ParseOpenAiCosts(string json) => AiPriceCalculator.ParseOpenAiCostsJson(json);

    /// <summary>
    /// OpenAI API bağlantı ve durum kontrolü (ve varsa Admin API üzerinden resmi fatura sorgulama)
    /// </summary>
    public static async Task<AiProviderBalanceInfo> CheckOpenAiStatusAsync(string apiKey, string? adminApiKey = null, CancellationToken ct = default)
    {
        var info = new AiProviderBalanceInfo
        {
            Provider = "OpenAI",
            MaskedApiKey = MaskApiKey(apiKey),
            CheckedAt = DateTimeOffset.Now
        };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            info.StatusMessage = "API Anahtarı eksik veya tanımlanmamış.";
            return info;
        }

        try
        {
            // 1. Standart model listesi ile doğrulama
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
                return info;
            }

            // 2. Admin API anahtarı veya kullanım yetkisi varsa resmi /v1/organization/costs sorgula
            string? effectiveAdminKey = !string.IsNullOrWhiteSpace(adminApiKey)
                ? adminApiKey.Trim()
                : (apiKey.Trim().StartsWith("sk-admin-", StringComparison.OrdinalIgnoreCase) ? apiKey.Trim() : null);

            if (!string.IsNullOrWhiteSpace(effectiveAdminKey))
            {
                try
                {
                    var startOfMonth = new DateTimeOffset(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
                    string costsUrl = $"https://api.openai.com/v1/organization/costs?start_time={startOfMonth}";

                    using var costReq = new HttpRequestMessage(HttpMethod.Get, costsUrl);
                    costReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveAdminKey);

                    using var costResp = await HttpClient.SendAsync(costReq, ct);
                    if (costResp.IsSuccessStatusCode)
                    {
                        var costBody = await costResp.Content.ReadAsStringAsync(ct);
                        decimal officialCost = ParseOpenAiCosts(costBody);
                        info.OfficialMonthlyCostUsd = officialCost;
                        info.HasAdminKey = true;
                        info.StatusMessage = $"✅ OpenAI Aktif | Bu Ayki Fatura: ${officialCost:N2} USD";
                    }
                }
                catch
                {
                    // Admin endpoint hatası ana bağlantıyı engellemez
                }
            }
        }
        catch (Exception ex)
        {
            info.StatusMessage = $"Bağlantı Hatası: {ex.Message}";
        }

        return info;
    }

    /// <summary>
    /// Google Gemini kota ve sağlık durumu kontrolü (GET https://generativelanguage.googleapis.com/v1beta/models)
    /// </summary>
    public static async Task<AiProviderBalanceInfo> CheckGeminiStatusAsync(string apiKey, CancellationToken ct = default)
    {
        var info = new AiProviderBalanceInfo
        {
            Provider = "Google Gemini",
            CheckedAt = DateTimeOffset.Now,
            MaskedApiKey = MaskApiKey(apiKey)
        };

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
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                info.IsAvailable = true;
                var models = AiPriceCalculator.ParseGeminiModelsJson(body);
                info.AvailableModels = models;
                info.StatusMessage = models.Count > 0
                    ? $"✅ Google Gemini Aktif ({models.Count} Model Hazır)"
                    : "✅ Google Gemini API Bağlantısı Aktif";
            }
            else
            {
                info.StatusMessage = AiPriceCalculator.ParseGeminiErrorJson(body, (int)resp.StatusCode);
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
