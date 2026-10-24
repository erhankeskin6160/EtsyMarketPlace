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

        var trimmedModel = (model ?? string.Empty).Trim();
        if (trimmedModel.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            return await CallOpenAiCompatibleOptimizationAsync(
                "https://api.deepseek.com/chat/completions",
                "DeepSeek",
                trimmedModel.Contains("reasoner", StringComparison.OrdinalIgnoreCase) ? "deepseek-reasoner" : "deepseek-chat",
                systemInstruction,
                userPrompt,
                apiKey,
                cancellationToken);
        }

        if (trimmedModel.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
            trimmedModel.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            trimmedModel.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            trimmedModel.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
        {
            return await CallOpenAiCompatibleOptimizationAsync(
                "https://api.openai.com/v1/chat/completions",
                "OpenAI",
                trimmedModel,
                systemInstruction,
                userPrompt,
                apiKey,
                cancellationToken);
        }

        if (trimmedModel.StartsWith("grok", StringComparison.OrdinalIgnoreCase))
        {
            return await CallOpenAiCompatibleOptimizationAsync(
                "https://api.x.ai/v1/chat/completions",
                "xAI Grok",
                trimmedModel,
                systemInstruction,
                userPrompt,
                apiKey,
                cancellationToken);
        }

        if (trimmedModel.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            return await CallClaudeOptimizationAsync(
                trimmedModel,
                systemInstruction,
                userPrompt,
                apiKey,
                cancellationToken);
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

    private async Task<(bool Success, string? Content, string? ErrorMessage)> CallOpenAiCompatibleOptimizationAsync(
        string endpointUrl,
        string providerLabel,
        string modelName,
        string systemInstruction,
        string userPrompt,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemInstruction + "\n\nIMPORTANT: Return ONLY valid JSON matching the requested schema. Do not wrap in markdown code fences." },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.35,
            response_format = new { type = "json_object" }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(req, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errMsg = $"{providerLabel} API Hatası (HTTP {(int)response.StatusCode}): {response.ReasonPhrase}";
            try
            {
                using var errDoc = JsonDocument.Parse(responseBody);
                if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                    errObj.TryGetProperty("message", out var mObj))
                {
                    errMsg = $"{providerLabel} API Hatası: {mObj.GetString()}";
                }
            }
            catch { }

            return (false, null, errMsg);
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var candidateText = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return (true, StripMarkdownJsonFences(candidateText), null);
        }
        catch (Exception ex)
        {
            return (false, null, $"{providerLabel} yanıtı işlenemedi: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Content, string? ErrorMessage)> CallClaudeOptimizationAsync(
        string modelName,
        string systemInstruction,
        string userPrompt,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = modelName,
            max_tokens = 4096,
            temperature = 0.35,
            system = systemInstruction + "\n\nIMPORTANT: Return ONLY valid JSON matching the requested schema. Do not wrap in markdown code fences.",
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(req, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errMsg = $"Claude API Hatası (HTTP {(int)response.StatusCode}): {response.ReasonPhrase}";
            try
            {
                using var errDoc = JsonDocument.Parse(responseBody);
                if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                    errObj.TryGetProperty("message", out var mObj))
                {
                    errMsg = $"Claude API Hatası: {mObj.GetString()}";
                }
            }
            catch { }

            return (false, null, errMsg);
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var candidateText = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            return (true, StripMarkdownJsonFences(candidateText), null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Claude yanıtı işlenemedi: {ex.Message}");
        }
    }

    private static string? StripMarkdownJsonFences(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            clean = clean.Substring(7);
        else if (clean.StartsWith("```", StringComparison.Ordinal))
            clean = clean.Substring(3);
        if (clean.EndsWith("```", StringComparison.Ordinal))
            clean = clean.Substring(0, clean.Length - 3);
        return clean.Trim();
    }

    private static void UnusedPlaceholder() { if (true)
        {
        }
    }
}
