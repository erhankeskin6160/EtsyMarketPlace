namespace SimilarProductsWinForms.Services;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tüm AI provider'larına (OpenAI, Gemini, Claude) birleşik çağrı arayüzü.
/// AiModelRouter'ın yönlendirme kararlarıyla birlikte kullanılır.
/// </summary>
internal static class AiProviderCaller
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    // =========================================================================
    // OpenAI
    // =========================================================================
    public static async Task<string> CallOpenAiAsync(
        string system, string user, string apiKey, string model,
        int maxTokens = 4096, double temperature = 0.5,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            max_tokens = maxTokens,
            temperature
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string err = ExtractErrorMessage(body, resp.ReasonPhrase ?? "İstek başarısız");
            return $"⚠️ OpenAI API Hatası (HTTP {(int)resp.StatusCode}): {err}";
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        return "⚠️ OpenAI geçerli bir yanıt içeriği döndürmedi.";
    }

    /// <summary>OpenAI Vision API çağrısı — base64 veya URL ile görsel gönderir.</summary>
    public static async Task<string> CallOpenAiVisionAsync(
        string system, string user, string apiKey, string model,
        string[] imageUrls, int maxTokens = 1000, double temperature = 0.4,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        var contentList = new System.Collections.Generic.List<object>
        {
            new { type = "text", text = $"{system}\n\n{user}" }
        };

        foreach (var url in imageUrls)
        {
            contentList.Add(new { type = "image_url", image_url = new { url, detail = "low" } });
        }

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = contentList }
            },
            max_tokens = maxTokens,
            temperature
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return "";

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        return "";
    }

    // =========================================================================
    // Google Gemini
    // =========================================================================
    public static async Task<string> CallGeminiAsync(
        string system, string user, string apiKey, string model,
        int maxTokens = 4096, double temperature = 0.5,
        CancellationToken ct = default)
    {
        string normalizedModel = AiModelNormalizer.NormalizeGeminiTextModel(model);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{normalizedModel}:generateContent?key={Uri.EscapeDataString(apiKey.Trim())}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);

        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[]
            {
                new { parts = new[] { new { text = user } } }
            },
            generationConfig = new { maxOutputTokens = maxTokens, temperature }
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            // Model adı bulunamadıysa (404) veya geçersiz modelse gemini-2.0-flash ile fallback dene
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound && normalizedModel != "gemini-2.0-flash")
            {
                try
                {
                    string fallbackUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={Uri.EscapeDataString(apiKey.Trim())}";
                    using var fbReq = new HttpRequestMessage(HttpMethod.Post, fallbackUrl)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                    };
                    using var fbResp = await Http.SendAsync(fbReq, ct);
                    var fbBody = await fbResp.Content.ReadAsStringAsync(ct);
                    if (fbResp.IsSuccessStatusCode)
                    {
                        using var fbDoc = JsonDocument.Parse(fbBody);
                        if (fbDoc.RootElement.TryGetProperty("candidates", out var fbCands) && fbCands.GetArrayLength() > 0)
                        {
                            string fbText = ExtractGeminiCandidateText(fbCands[0], "gemini-2.0-flash");
                            if (!string.IsNullOrWhiteSpace(fbText)) return fbText;
                        }
                    }
                }
                catch { }
            }

            string err = ExtractErrorMessage(body, resp.ReasonPhrase ?? "İstek başarısız");
            return $"⚠️ Google Gemini API Hatası (HTTP {(int)resp.StatusCode}): {err}";
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            string text = ExtractGeminiCandidateText(candidates[0], normalizedModel);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return "⚠️ Google Gemini yanıt üretemedi (Filtrelenmiş olabilir).";
    }

    private static string ExtractGeminiCandidateText(JsonElement candidate, string normalizedModel)
    {
        var sbAnswer = new StringBuilder();
        var sbThought = new StringBuilder();

        if (candidate.TryGetProperty("content", out var contentEl) && contentEl.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
            {
                bool isThought = false;
                if (part.TryGetProperty("thought", out var tProp) && tProp.GetBoolean())
                {
                    isThought = true;
                }

                if (part.TryGetProperty("text", out var textProp))
                {
                    string t = textProp.GetString() ?? "";
                    if (isThought)
                    {
                        sbThought.Append(t);
                    }
                    else
                    {
                        sbAnswer.Append(t);
                    }
                }
            }
        }

        string answer = sbAnswer.ToString().Trim();
        string thought = sbThought.ToString().Trim();

        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = thought;
        }
        else if (!string.IsNullOrWhiteSpace(thought) && normalizedModel.Contains("thinking", StringComparison.OrdinalIgnoreCase))
        {
            answer = $"💭 [Gemini Düşünce Akışı]:\n{thought}\n\n━━━━━━━━━━━━━━━━━━━━\n\n🎯 [Doktor Teşhis & Reçete]:\n{answer}";
        }

        return answer;
    }

    // =========================================================================
    // Anthropic Claude
    // =========================================================================
    public static async Task<string> CallClaudeAsync(
        string system, string user, string apiKey, string model,
        int maxTokens = 4096, double temperature = 0.5,
        CancellationToken ct = default)
    {
        string normalizedModel = AiModelNormalizer.NormalizeClaudeTextModel(model);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", apiKey.Trim());
        req.Headers.Add("anthropic-version", "2023-06-01");

        var payload = new
        {
            model = normalizedModel,
            max_tokens = maxTokens,
            temperature,
            system,
            messages = new[]
            {
                new { role = "user", content = user }
            }
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string err = ExtractErrorMessage(body, resp.ReasonPhrase ?? "İstek başarısız");
            return $"⚠️ Claude API Hatası (HTTP {(int)resp.StatusCode}): {err}";
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
        {
            return content[0].GetProperty("text").GetString() ?? "";
        }
        return "⚠️ Claude geçerli bir yanıt içeriği döndürmedi.";
    }

    private static string ExtractErrorMessage(string body, string defaultError)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorEl))
            {
                if (errorEl.ValueKind == JsonValueKind.String)
                    return errorEl.GetString() ?? defaultError;
                if (errorEl.TryGetProperty("message", out var msgEl))
                    return msgEl.GetString() ?? defaultError;
            }
        }
        catch { }
        return defaultError;
    }

    /// <summary>Claude Vision API — base64 görsel gönderimli multimodal çağrı.</summary>
    public static async Task<string> CallClaudeVisionAsync(
        string system, string userText, string apiKey, string model,
        byte[][] imageDataList, string[] mediaTypes,
        int maxTokens = 1200, double temperature = 0.4,
        CancellationToken ct = default)
    {
        string normalizedModel = AiModelNormalizer.NormalizeClaudeTextModel(model);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", apiKey.Trim());
        req.Headers.Add("anthropic-version", "2023-06-01");

        var contentParts = new System.Collections.Generic.List<object>();

        // Görselleri ekle (Claude formatı: base64 source)
        for (int i = 0; i < imageDataList.Length && i < 10; i++)
        {
            contentParts.Add(new
            {
                type = "image",
                source = new
                {
                    type = "base64",
                    media_type = i < mediaTypes.Length ? mediaTypes[i] : "image/jpeg",
                    data = Convert.ToBase64String(imageDataList[i])
                }
            });
        }

        // Metin talimatını ekle
        contentParts.Add(new { type = "text", text = userText });

        var payload = new
        {
            model = normalizedModel,
            max_tokens = maxTokens,
            temperature,
            system,
            messages = new[]
            {
                new { role = "user", content = contentParts }
            }
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return "";

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
        {
            return content[0].GetProperty("text").GetString() ?? "";
        }
        return "";
    }

    // =========================================================================
    // DeepSeek (R1 Reasoner & V3 Chat)
    // =========================================================================
    public static async Task<string> CallDeepSeekAsync(
        string system, string user, string apiKey, string model,
        int maxTokens = 4096, double temperature = 0.6,
        CancellationToken ct = default)
    {
        string normalizedModel = AiModelNormalizer.NormalizeDeepSeekModel(model);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        var payload = new
        {
            model = normalizedModel,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            max_tokens = maxTokens,
            temperature
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string err = ExtractErrorMessage(body, resp.ReasonPhrase ?? "İstek başarısız");
            return $"⚠️ DeepSeek API Hatası (HTTP {(int)resp.StatusCode}): {err}";
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var msg = choices[0].GetProperty("message");
            string text = msg.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";
            string reasoning = msg.TryGetProperty("reasoning_content", out var rEl) ? rEl.GetString() ?? "" : "";

            if (!string.IsNullOrWhiteSpace(reasoning) && !string.IsNullOrWhiteSpace(text))
            {
                return $"💭 [DeepSeek Düşünce Akışı]:\n{reasoning}\n\n━━━━━━━━━━━━━━━━━━━━\n\n🎯 [Doktor Teşhis & Reçete]:\n{text}";
            }
            return !string.IsNullOrWhiteSpace(text) ? text : reasoning;
        }
        return "⚠️ DeepSeek geçerli bir yanıt içeriği döndürmedi.";
    }

    // =========================================================================
    // xAI Grok (Grok-3 & Grok-2)
    // =========================================================================
    public static async Task<string> CallGrokAsync(
        string system, string user, string apiKey, string model,
        int maxTokens = 4096, double temperature = 0.5,
        CancellationToken ct = default)
    {
        string normalizedModel = AiModelNormalizer.NormalizeGrokModel(model);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        var payload = new
        {
            model = normalizedModel,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            max_tokens = maxTokens,
            temperature
        };

        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string err = ExtractErrorMessage(body, resp.ReasonPhrase ?? "İstek başarısız");
            return $"⚠️ xAI Grok API Hatası (HTTP {(int)resp.StatusCode}): {err}";
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        return "⚠️ xAI Grok geçerli bir yanıt içeriği döndürmedi.";
    }
}
