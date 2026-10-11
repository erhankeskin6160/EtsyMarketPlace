namespace EtsyMarketPlace.Infrastructure.ListingOptimization;

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;

public sealed class GeminiListingApiClient : IGeminiListingApiClient
{
    private readonly HttpClient _httpClient;

    public GeminiListingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
        string systemInstruction,
        string userPrompt,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, null, "Gemini API anahtarı boş olamaz.");
        }

        var normalizedModel = EtsyAiModelNormalizer.NormalizeGeminiTextModel(model);
        var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(normalizedModel)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemInstruction } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.35,
                topP = 0.95,
                maxOutputTokens = 4096,
                responseMimeType = "application/json"
            }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(geminiUrl, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errMsg = $"Google Gemini API Hatası (HTTP {(int)response.StatusCode}): {response.ReasonPhrase}";
            try
            {
                using var errDoc = JsonDocument.Parse(responseBody);
                if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                    errObj.TryGetProperty("message", out var mObj))
                {
                    errMsg = $"Google Gemini API Hatası: {mObj.GetString()}";
                }
            }
            catch { }

            return (false, null, errMsg);
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var candidateText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return (true, candidateText, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Gemini yanıtı işlenemedi: {ex.Message}");
        }
    }
}
