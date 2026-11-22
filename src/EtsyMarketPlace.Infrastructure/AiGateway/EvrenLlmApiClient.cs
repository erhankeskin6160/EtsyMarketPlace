namespace EtsyMarketPlace.Infrastructure.AiGateway;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiGateway;

public sealed class EvrenLlmApiClient : IEvrenLlmApiClient
{
    private const string ChatCompletionsUrl = "https://evren-llmapi.ssyz.org.tr/v1/chat/completions";
    private const string ModelsUrl = "https://evren-llmapi.ssyz.org.tr/v1/models";

    private readonly HttpClient _httpClient;

    public EvrenLlmApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<(bool Success, IReadOnlyList<string> Models, string? ErrorMessage, int StatusCode)> ListModelsAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var trimmedKey = apiKey.Trim();
            using var req = new HttpRequestMessage(HttpMethod.Get, ModelsUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", trimmedKey);
            req.Headers.TryAddWithoutValidation("X-API-Key", trimmedKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                var err = ExtractErrorMessage(body, resp.ReasonPhrase ?? $"HTTP {(int)resp.StatusCode}");
                return (false, Array.Empty<string>(), $"EVREN API Hatası (HTTP {(int)resp.StatusCode}): {err}", (int)resp.StatusCode);
            }

            var models = new List<string>();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataArr.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                    {
                        var id = idEl.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(id))
                            models.Add(id);
                    }
                }
            }

            return (true, models, null, (int)resp.StatusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, Array.Empty<string>(), "EVREN API bağlantı zaman aşımına uğradı.", 504);
        }
        catch (Exception ex)
        {
            return (false, Array.Empty<string>(), $"EVREN API sunucusuna erişilemedi: {ex.Message}", 502);
        }
    }

    public async Task<EvrenChatCompletionResult> SendChatCompletionAsync(
        EvrenChatCompletionCommand command,
        CancellationToken cancellationToken = default)
    {
        var model = EvrenAiGatewayService.NormalizeEvrenModel(command.Model);
        try
        {
            var messagesPayload = new List<Dictionary<string, object>>();
            foreach (var msg in command.Messages)
            {
                messagesPayload.Add(new Dictionary<string, object>
                {
                    ["role"] = string.IsNullOrWhiteSpace(msg.Role) ? "user" : msg.Role.Trim(),
                    ["content"] = msg.Content
                });
            }

            var payloadDict = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = messagesPayload
            };

            if (command.Temperature.HasValue)
            {
                payloadDict["temperature"] = Math.Clamp(command.Temperature.Value, 0.0, 2.0);
            }

            if (command.MaxTokens.HasValue && command.MaxTokens.Value > 0)
            {
                payloadDict["max_tokens"] = Math.Clamp(command.MaxTokens.Value, 64, 16384);
            }

            if (command.JsonMode)
            {
                payloadDict["response_format"] = new { type = "json_object" };
            }

            var trimmedKey = command.ApiKey.Trim();
            using var req = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", trimmedKey);
            req.Headers.TryAddWithoutValidation("X-API-Key", trimmedKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(JsonSerializer.Serialize(payloadDict), Encoding.UTF8, "application/json");

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                var statusCode = (int)resp.StatusCode;
                var err = ExtractErrorMessage(body, resp.ReasonPhrase ?? $"HTTP {statusCode}");
                var formattedError = statusCode == 401 || statusCode == 403
                    ? $"EVREN API Anahtarı Reddedildi (HTTP {statusCode}): {err}. Lütfen evren.ssyz.org.tr üzerinden oluşturduğunuz 'evren_llm_...' anahtarının tam ve aktif olduğunu doğrulayın."
                    : $"EVREN API Hatası (HTTP {statusCode}): {err}";
                return new EvrenChatCompletionResult(
                    Success: false,
                    Content: null,
                    ReasoningContent: null,
                    ErrorMessage: formattedError,
                    StatusCode: statusCode,
                    ResolvedModel: model,
                    PromptTokens: 0,
                    CompletionTokens: 0,
                    TotalTokens: 0);
            }

            using var doc = JsonDocument.Parse(body);
            string? content = null;
            string? reasoning = null;

            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var messageEl = choices[0].GetProperty("message");
                if (messageEl.TryGetProperty("content", out var cEl) && cEl.ValueKind == JsonValueKind.String)
                {
                    content = cEl.GetString();
                }

                if (messageEl.TryGetProperty("reasoning_content", out var rEl) && rEl.ValueKind == JsonValueKind.String)
                {
                    reasoning = rEl.GetString();
                }
            }

            if (command.JsonMode && !string.IsNullOrWhiteSpace(content))
            {
                content = StripMarkdownJsonFences(content);
            }

            int promptTokens = 0, completionTokens = 0, totalTokens = 0;
            if (doc.RootElement.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.TryGetInt32(out var pVal)) promptTokens = pVal;
                if (usage.TryGetProperty("completion_tokens", out var ct) && ct.TryGetInt32(out var cVal)) completionTokens = cVal;
                if (usage.TryGetProperty("total_tokens", out var tt) && tt.TryGetInt32(out var tVal)) totalTokens = tVal;
                if (totalTokens == 0) totalTokens = promptTokens + completionTokens;
            }

            return new EvrenChatCompletionResult(
                Success: true,
                Content: content ?? string.Empty,
                ReasoningContent: reasoning,
                ErrorMessage: null,
                StatusCode: (int)resp.StatusCode,
                ResolvedModel: model,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TotalTokens: totalTokens);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new EvrenChatCompletionResult(false, null, null, "EVREN API isteği zaman aşımına uğradı.", 504, model, 0, 0, 0);
        }
        catch (Exception ex)
        {
            return new EvrenChatCompletionResult(false, null, null, $"EVREN API bağlantı hatası: {ex.Message}", 502, model, 0, 0, 0);
        }
    }

    private static string ExtractErrorMessage(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body)) return fallback;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.Object &&
                    err.TryGetProperty("message", out var msg) &&
                    msg.ValueKind == JsonValueKind.String)
                {
                    return msg.GetString() ?? fallback;
                }
                if (err.ValueKind == JsonValueKind.String)
                {
                    return err.GetString() ?? fallback;
                }
            }
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString() ?? fallback;
            }
        }
        catch { }

        return body.Length > 260 ? body[..260] : body;
    }

    private static string StripMarkdownJsonFences(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }
        }
        return trimmed.Trim();
    }
}
