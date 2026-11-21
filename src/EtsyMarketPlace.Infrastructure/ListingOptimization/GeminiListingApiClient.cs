namespace EtsyMarketPlace.Infrastructure.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;
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
        var result = await GenerateOptimizationJsonWithUsageAsync(
            systemInstruction,
            userPrompt,
            model,
            apiKey,
            maxOutputTokens: null,
            cancellationToken);
        return (result.Success, result.Content, result.ErrorMessage);
    }

    public async Task<LlmJsonResult> GenerateOptimizationJsonWithUsageAsync(
        string systemInstruction,
        string userPrompt,
        string model,
        string apiKey,
        int? maxOutputTokens = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new LlmJsonResult(false, null, "Gemini API anahtarı boş olamaz.", 0, 0, 0, "Google Gemini", model ?? string.Empty);
        }

        var trimmedModel = (model ?? string.Empty).Trim();
        if (trimmedModel.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            var resolvedDeepSeekModel = trimmedModel.Contains("reasoner", StringComparison.OrdinalIgnoreCase)
                ? "deepseek-reasoner"
                : "deepseek-chat";
            return await CallOpenAiCompatibleOptimizationAsync(
                "https://api.deepseek.com/chat/completions",
                "DeepSeek",
                resolvedDeepSeekModel,
                systemInstruction,
                userPrompt,
                apiKey,
                maxOutputTokens,
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
                maxOutputTokens,
                cancellationToken);
        }

        if (trimmedModel.StartsWith("grok", StringComparison.OrdinalIgnoreCase))
        {
            return await CallOpenAiCompatibleOptimizationAsync(
                "https://api.x.ai/v1/chat/completions",
                "Grok",
                trimmedModel,
                systemInstruction,
                userPrompt,
                apiKey,
                maxOutputTokens,
                cancellationToken);
        }

        if (trimmedModel.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            return await CallClaudeOptimizationAsync(
                trimmedModel,
                systemInstruction,
                userPrompt,
                apiKey,
                maxOutputTokens,
                cancellationToken);
        }

        var normalizedModel = EtsyAiModelNormalizer.NormalizeGeminiTextModel(model);
        var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(normalizedModel)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        var effectiveMaxTokens = maxOutputTokens.HasValue && maxOutputTokens.Value > 0
            ? Math.Clamp(maxOutputTokens.Value, 128, 8192)
            : 4096;

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
                maxOutputTokens = effectiveMaxTokens,
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

            return new LlmJsonResult(false, null, errMsg, 0, 0, 0, "Google Gemini", normalizedModel);
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

            var (promptTokens, completionTokens, totalTokens) = ExtractGeminiUsage(doc.RootElement);
            return new LlmJsonResult(true, candidateText, null, promptTokens, completionTokens, totalTokens, "Google Gemini", normalizedModel);
        }
        catch (Exception ex)
        {
            return new LlmJsonResult(false, null, $"Gemini yanıtı işlenemedi: {ex.Message}", 0, 0, 0, "Google Gemini", normalizedModel);
        }
    }

    public async Task<(bool Success, string? Content, string? ErrorMessage)> GenerateVisionJsonAsync(
        string systemInstruction,
        string userPrompt,
        IReadOnlyList<VisionImageInput> images,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, null, "AI API anahtarı boş olamaz.");

        var trimmedModel = (model ?? string.Empty).Trim();

        // DeepSeek text-only models do not support vision inputs -> use text optimization gracefully
        if (trimmedModel.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            return await GenerateOptimizationJsonAsync(systemInstruction, userPrompt, trimmedModel, apiKey, cancellationToken);
        }

        var loadedImages = await ResolveImageBytesAsync(images, cancellationToken);
        if (loadedImages.Count == 0)
        {
            return await GenerateOptimizationJsonAsync(systemInstruction, userPrompt, trimmedModel, apiKey, cancellationToken);
        }

        if (trimmedModel.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
            trimmedModel.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            trimmedModel.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            trimmedModel.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
            trimmedModel.StartsWith("grok", StringComparison.OrdinalIgnoreCase))
        {
            var isGrok = trimmedModel.StartsWith("grok", StringComparison.OrdinalIgnoreCase);
            var endpoint = isGrok ? "https://api.x.ai/v1/chat/completions" : "https://api.openai.com/v1/chat/completions";
            var contentParts = new List<object> { new { type = "text", text = userPrompt } };
            foreach (var img in loadedImages)
            {
                var dataUri = $"data:{img.MimeType};base64,{Convert.ToBase64String(img.Bytes)}";
                contentParts.Add(new { type = "image_url", image_url = new { url = dataUri, detail = "low" } });
            }

            var payload = new
            {
                model = trimmedModel,
                messages = new object[]
                {
                    new { role = "system", content = systemInstruction },
                    new { role = "user", content = contentParts }
                },
                temperature = 0.35,
                max_tokens = 2500,
                response_format = new { type = "json_object" }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
                return (false, null, $"Vision API Hatası (HTTP {(int)resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return (true, text, null);
        }

        // Default: Google Gemini multimodal generateContent
        var normalizedModel = EtsyAiModelNormalizer.NormalizeGeminiTextModel(trimmedModel);
        var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(normalizedModel)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var geminiParts = new List<object> { new { text = userPrompt } };
        foreach (var img in loadedImages)
        {
            geminiParts.Add(new
            {
                inline_data = new
                {
                    mime_type = img.MimeType,
                    data = Convert.ToBase64String(img.Bytes)
                }
            });
        }

        var geminiPayload = new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = new[] { new { role = "user", parts = geminiParts } },
            generationConfig = new
            {
                temperature = 0.35,
                topP = 0.95,
                maxOutputTokens = 3072,
                responseMimeType = "application/json"
            }
        };

        using var gContent = new StringContent(JsonSerializer.Serialize(geminiPayload), Encoding.UTF8, "application/json");
        using var gResp = await _httpClient.PostAsync(geminiUrl, gContent, cancellationToken);
        var gBody = await gResp.Content.ReadAsStringAsync(cancellationToken);
        if (!gResp.IsSuccessStatusCode)
            return (false, null, $"Google Gemini Vision API Hatası (HTTP {(int)gResp.StatusCode}): {gBody}");

        using var gDoc = JsonDocument.Parse(gBody);
        var candidateText = gDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        return (true, candidateText, null);
    }

    private async Task<List<(byte[] Bytes, string MimeType)>> ResolveImageBytesAsync(
        IReadOnlyList<VisionImageInput> inputs,
        CancellationToken ct)
    {
        var results = new List<(byte[] Bytes, string MimeType)>();
        foreach (var input in inputs.Take(10))
        {
            if (input.Bytes != null && input.Bytes.Length > 0)
            {
                results.Add((input.Bytes, string.IsNullOrWhiteSpace(input.MimeType) ? "image/jpeg" : input.MimeType));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(input.ImageUrl) &&
                (input.ImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 input.ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using var resp = await _httpClient.GetAsync(input.ImageUrl, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                        var mime = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                        if (bytes.Length > 0) results.Add((bytes, mime));
                    }
                }
                catch { }
            }
        }
        return results;
    }

    private async Task<LlmJsonResult> CallOpenAiCompatibleOptimizationAsync(
        string endpointUrl,
        string providerLabel,
        string modelName,
        string systemInstruction,
        string userPrompt,
        string apiKey,
        int? maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var payloadDict = new Dictionary<string, object>
        {
            ["model"] = modelName,
            ["messages"] = new[]
            {
                new { role = "system", content = systemInstruction + "\n\nIMPORTANT: Return ONLY valid JSON matching the requested schema. Do not wrap in markdown code fences." },
                new { role = "user", content = userPrompt }
            },
            ["temperature"] = 0.35,
            ["response_format"] = new { type = "json_object" }
        };
        if (maxOutputTokens.HasValue && maxOutputTokens.Value > 0)
        {
            payloadDict["max_tokens"] = Math.Clamp(maxOutputTokens.Value, 128, 8192);
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payloadDict), Encoding.UTF8, "application/json");

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

            return new LlmJsonResult(false, null, errMsg, 0, 0, 0, providerLabel, modelName);
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var candidateText = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var (promptTokens, completionTokens, totalTokens) = ExtractOpenAiUsage(doc.RootElement);
            return new LlmJsonResult(true, StripMarkdownJsonFences(candidateText), null, promptTokens, completionTokens, totalTokens, providerLabel, modelName);
        }
        catch (Exception ex)
        {
            return new LlmJsonResult(false, null, $"{providerLabel} yanıtı işlenemedi: {ex.Message}", 0, 0, 0, providerLabel, modelName);
        }
    }

    private async Task<LlmJsonResult> CallClaudeOptimizationAsync(
        string modelName,
        string systemInstruction,
        string userPrompt,
        string apiKey,
        int? maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var effectiveMaxTokens = maxOutputTokens.HasValue && maxOutputTokens.Value > 0
            ? Math.Clamp(maxOutputTokens.Value, 128, 8192)
            : 4096;

        var payload = new
        {
            model = modelName,
            max_tokens = effectiveMaxTokens,
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

            return new LlmJsonResult(false, null, errMsg, 0, 0, 0, "Claude", modelName);
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var candidateText = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            var (promptTokens, completionTokens, totalTokens) = ExtractClaudeUsage(doc.RootElement);
            return new LlmJsonResult(true, StripMarkdownJsonFences(candidateText), null, promptTokens, completionTokens, totalTokens, "Claude", modelName);
        }
        catch (Exception ex)
        {
            return new LlmJsonResult(false, null, $"Claude yanıtı işlenemedi: {ex.Message}", 0, 0, 0, "Claude", modelName);
        }
    }

    private static (int Prompt, int Completion, int Total) ExtractOpenAiUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return (0, 0, 0);

        var prompt = usage.TryGetProperty("prompt_tokens", out var p) && p.TryGetInt32(out var pv) ? pv : 0;
        var completion = usage.TryGetProperty("completion_tokens", out var c) && c.TryGetInt32(out var cv) ? cv : 0;
        var total = usage.TryGetProperty("total_tokens", out var t) && t.TryGetInt32(out var tv) ? tv : prompt + completion;
        return (prompt, completion, total);
    }

    private static (int Prompt, int Completion, int Total) ExtractGeminiUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return (0, 0, 0);

        var prompt = usage.TryGetProperty("promptTokenCount", out var p) && p.TryGetInt32(out var pv) ? pv : 0;
        var completion = usage.TryGetProperty("candidatesTokenCount", out var c) && c.TryGetInt32(out var cv) ? cv : 0;
        var total = usage.TryGetProperty("totalTokenCount", out var t) && t.TryGetInt32(out var tv) ? tv : prompt + completion;
        return (prompt, completion, total);
    }

    private static (int Prompt, int Completion, int Total) ExtractClaudeUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return (0, 0, 0);

        var prompt = usage.TryGetProperty("input_tokens", out var p) && p.TryGetInt32(out var pv) ? pv : 0;
        var completion = usage.TryGetProperty("output_tokens", out var c) && c.TryGetInt32(out var cv) ? cv : 0;
        return (prompt, completion, prompt + completion);
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
