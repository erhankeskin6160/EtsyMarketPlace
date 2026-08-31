namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// AI API Çağrıları için Kurumsal Dayanıklılık ve Fallback Yöneticisi (Resilience & Failover Handler)
/// - HTTP 503 (High Demand), 429 (Rate Limit), 500, 502, 504 ve Zaman Aşımı durumlarında Exponential Backoff ile Retry yapar.
/// - Model meşgul veya ulaşılamaz olduğunda otomatik olarak yedek model zincirini (Cascade Fallback) dener.
/// - Ham JSON hata mesajlarını temizleyerek kullanıcı dostu Türkçe bilgilendirme üretir.
/// </summary>
internal static class AiResilienceInvoker
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(35)
    };

    /// <summary>
    /// Gemini API'sine hataya dayanıklı (resilient) istek gönderir.
    /// Belirtilen model 503/429/timeout verirse otomatik olarak yedek modelleri sırayla dener.
    /// </summary>
    public static async Task<string> ExecuteGeminiWithFallbackAsync(
        string apiKey,
        string requestedModel,
        object payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API anahtarı girilmemiş.");
        }

        string cleanKey = apiKey.Trim();
        string primaryModel = AiModelNormalizer.NormalizeGeminiTextModel(requestedModel);

        // Fallback Modelleri Listesi (Birincil Model -> Yedek Hızlı Modeller)
        var modelCandidateChain = new List<string> { primaryModel };
        var backupModels = new[] { "gemini-2.5-flash", "gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-pro" };

        foreach (var backup in backupModels)
        {
            if (!modelCandidateChain.Contains(backup))
            {
                modelCandidateChain.Add(backup);
            }
        }

        Exception? lastException = null;

        foreach (var currentModel in modelCandidateChain)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:generateContent?key={cleanKey}";
            const int maxRetriesPerModel = 2;

            for (int attempt = 1; attempt <= maxRetriesPerModel; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json");

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(25));

                    using var response = await HttpClient.SendAsync(request, cts.Token);
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        return responseBody;
                    }

                    int statusCode = (int)response.StatusCode;
                    bool isTransient = statusCode == 503 || statusCode == 429 || statusCode == 500 || statusCode == 502 || statusCode == 504;

                    if (isTransient && attempt < maxRetriesPerModel)
                    {
                        // 1. denemede 1.2 sn bekle, tekrar dene
                        await Task.Delay(1200 * attempt, cancellationToken);
                        continue;
                    }

                    // Transient hata devam ediyorsa veya model bulunamadıysa bir sonraki modele geç
                    string friendlyError = SanitizeApiErrorMessage(responseBody, statusCode, currentModel);
                    lastException = new InvalidOperationException(friendlyError);
                    break; // sonraki modele geç
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // İstek zaman aşımına uğradı, bir sonraki modeli dene
                    lastException = new TimeoutException($"Gemini ({currentModel}) yanıt süresi doldu.");
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastException = ex;
                    if (attempt < maxRetriesPerModel)
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }
                    break;
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Gemini modellerine ulaşılamadı.");
    }

    /// <summary>
    /// OpenAI API'sine hataya dayanıklı istek gönderir.
    /// </summary>
    public static async Task<string> ExecuteOpenAiWithFallbackAsync(
        string apiKey,
        string requestedModel,
        object payload,
        string endpoint = "https://api.openai.com/v1/chat/completions",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API anahtarı girilmemiş.");
        }

        string cleanKey = apiKey.Trim();
        const int maxRetries = 2;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cleanKey);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                using var response = await HttpClient.SendAsync(request, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return responseBody;
                }

                int statusCode = (int)response.StatusCode;
                if ((statusCode == 429 || statusCode >= 500) && attempt < maxRetries)
                {
                    await Task.Delay(1500 * attempt, cancellationToken);
                    continue;
                }

                string friendlyError = SanitizeApiErrorMessage(responseBody, statusCode, requestedModel);
                throw new InvalidOperationException(friendlyError);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                if (attempt < maxRetries)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
                break;
            }
        }

        throw lastException ?? new InvalidOperationException("OpenAI servisine ulaşılamadı.");
    }

    /// <summary>
    /// Ham JSON hata mesajlarını ayrıştırarak temiz ve anlaşılır Türkçe mesaja dönüştürür.
    /// </summary>
    public static string SanitizeApiErrorMessage(string rawResponse, int statusCode, string model)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return $"HTTP {statusCode} hatası alındı ({model}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            if (doc.RootElement.TryGetProperty("error", out var errObj))
            {
                string msg = "";
                if (errObj.TryGetProperty("message", out var msgElem))
                {
                    msg = msgElem.GetString() ?? "";
                }

                if (statusCode == 503 || msg.Contains("high demand", StringComparison.OrdinalIgnoreCase) || msg.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Google Gemini sunucularında geçici yoğunluk yaşanıyor (HTTP 503). Model: {model}";
                }

                if (statusCode == 429 || msg.Contains("quota", StringComparison.OrdinalIgnoreCase) || msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                {
                    return $"API istek kotası / hız sınırı aşıldı (HTTP 429). Lütfen biraz bekleyin.";
                }

                if (statusCode == 404)
                {
                    return $"'{model}' modeli bulunamadı veya bu API anahtarı için yetkili değil (HTTP 404).";
                }

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    return $"{msg} (HTTP {statusCode})";
                }
            }
        }
        catch
        {
            // JSON parse edilemediyse
        }

        if (statusCode == 503)
        {
            return $"AI sunucularında geçici yoğunluk (HTTP 503 - High Demand). Model: {model}";
        }

        if (rawResponse.Length > 100)
        {
            return $"HTTP {statusCode} ({model}): {rawResponse[..95]}...";
        }

        return $"HTTP {statusCode} ({model}): {rawResponse}";
    }
}
