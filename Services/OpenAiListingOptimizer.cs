namespace SimilarProductsWinForms.Services;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;

internal sealed class OpenAiListingOptimizer(
    Func<AiOptimizationSettings> loadSettings,
    ListingOptimizationService localOptimizer) : IAiListingOptimizer
{
    private static readonly HttpClient HttpClient = new();

    public async Task<ListingOptimizationResult> OptimizeAsync(
        ListingOptimizationInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = loadSettings();
        if (settings.IsOffline)
        {
            var res = localOptimizer.Optimize(input);
            return res with
            {
                ExecutedProvider = "Offline",
                ExecutedModel = "Kural Tabanlı"
            };
        }

        if (settings.UseGemini)
        {
            return await OptimizeWithGeminiAsync(settings, input, cancellationToken);
        }

        if (settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Gemini API key girilmemis. AI Ayarlari ekraninda Gemini key alanini doldurun.");
        }

        if (settings.UseOpenAi)
        {
            return await OptimizeWithOpenAiAsync(settings, input, cancellationToken);
        }

        if (settings.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("OpenAI API key girilmemis. AI Ayarlari ekraninda OpenAI key alanini doldurun.");
        }

        if (settings.UseClaude)
        {
            return await OptimizeWithProviderAsync(
                "Claude", settings.ClaudeModel, settings.AllowSilentOfflineFallback, input,
                (sys, usr, ct) => AiProviderCaller.CallClaudeAsync(sys, usr, settings.ClaudeApiKey, settings.ClaudeModel, 4096, 0.4, ct),
                cancellationToken);
        }

        if (settings.Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Claude API key girilmemis. AI Ayarlari ekraninda Claude key alanini doldurun.");
        }

        if (settings.UseDeepSeek)
        {
            return await OptimizeWithProviderAsync(
                "DeepSeek", settings.DeepSeekModel, settings.AllowSilentOfflineFallback, input,
                (sys, usr, ct) => AiProviderCaller.CallDeepSeekAsync(sys, usr, settings.DeepSeekApiKey, settings.DeepSeekModel, 4096, 0.3, ct),
                cancellationToken);
        }

        if (settings.Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DeepSeek API key girilmemis. AI Ayarlari ekraninda DeepSeek key alanini doldurun.");
        }

        if (settings.UseGrok)
        {
            return await OptimizeWithProviderAsync(
                "Grok", settings.GrokModel, settings.AllowSilentOfflineFallback, input,
                (sys, usr, ct) => AiProviderCaller.CallGrokAsync(sys, usr, settings.GrokApiKey, settings.GrokModel, 4096, 0.4, ct),
                cancellationToken);
        }

        if (settings.Provider.Equals("Grok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Grok API key girilmemis. AI Ayarlari ekraninda Grok key alanini doldurun.");
        }

        var fallbackLocal = localOptimizer.Optimize(input);
        return fallbackLocal with
        {
            ExecutedProvider = "Offline",
            ExecutedModel = "Kural Tabanlı"
        };
    }

    private async Task<ListingOptimizationResult> OptimizeWithOpenAiAsync(
        AiOptimizationSettings settings,
        ListingOptimizationInput input,
        CancellationToken cancellationToken)
    {
        var local = localOptimizer.Optimize(input);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAiApiKey.Trim());
            request.Content = new StringContent(
                JsonSerializer.Serialize(CreateOpenAiPayload(settings.OpenAiModel, input)),
                Encoding.UTF8,
                "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"OpenAI istegi basarisiz. HTTP {(int)response.StatusCode}: {body}");
            }

            var outputText = StripJsonFences(ExtractOutputText(body));
            var ai = JsonSerializer.Deserialize<AiListingOptimizationResponse>(
                outputText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("OpenAI yaniti JSON olarak okunamadi.");

            return new ListingOptimizationResult(
                local.CurrentSeoScore,
                Math.Max(local.OptimizedSeoScore, Math.Min(100, local.CurrentSeoScore + 12)),
                NormalizeTitles(ai.TitleSuggestions, local.TitleSuggestions),
                NormalizeTags(ai.TagSuggestions, local.TagSuggestions),
                NormalizeMaterials(ai.MaterialSuggestions, local.MaterialSuggestions),
                string.IsNullOrWhiteSpace(ai.DescriptionDraft) ? local.DescriptionDraft : ai.DescriptionDraft.Trim(),
                local.MissingTerms,
                NormalizeList(ai.RiskWarnings, local.RiskWarnings),
                local.ActionChecklist.Concat(["AI onerisi yayinlanmadan once marka/telif ve Etsy politika kontrolunden gecir."]).Distinct().ToList(),
                ExecutedProvider: "OpenAI",
                ExecutedModel: settings.OpenAiModel,
                IsFallback: false);
        }
        catch (Exception ex)
        {
            if (!settings.AllowSilentOfflineFallback)
            {
                throw new InvalidOperationException($"Seçtiğiniz OpenAI modeli ({settings.OpenAiModel}) yanıt veremedi: {ex.Message}\n\nLütfen tekrar deneyin veya AI Ayarları ekranından farklı bir model seçin.");
            }
            return CreateLocalFallbackResult(local, ex.Message, "OpenAI", settings.OpenAiModel);
        }
    }

    private async Task<ListingOptimizationResult> OptimizeWithGeminiAsync(
        AiOptimizationSettings settings,
        ListingOptimizationInput input,
        CancellationToken cancellationToken)
    {
        var local = localOptimizer.Optimize(input);
        AiListingOptimizationResponse? ai = null;
        try
        {
            var body = await SendGeminiRequestAsync(settings, input, cancellationToken);
            var outputText = StripJsonFences(ExtractGeminiOutputText(body));
            ai = JsonSerializer.Deserialize<AiListingOptimizationResponse>(
                outputText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            if (!settings.AllowSilentOfflineFallback)
            {
                throw new InvalidOperationException($"Seçtiğiniz Gemini modeli ({settings.GeminiModel}) yanıt veremedi: {ex.Message}\n\nLütfen biraz sonra tekrar deneyin veya AI Ayarları ekranından farklı bir model seçin.");
            }
            return CreateLocalFallbackResult(local, ex.Message, "Gemini", settings.GeminiModel);
        }

        if (ai is null)
        {
            if (!settings.AllowSilentOfflineFallback)
            {
                throw new InvalidOperationException($"Gemini ({settings.GeminiModel}) yanıtı JSON olarak okunamadı. Lütfen tekrar deneyin.");
            }
            return CreateLocalFallbackResult(local, "Gemini yaniti JSON olarak okunamadi.", "Gemini", settings.GeminiModel);
        }

        return new ListingOptimizationResult(
            local.CurrentSeoScore,
            Math.Max(local.OptimizedSeoScore, Math.Min(100, local.CurrentSeoScore + 12)),
            NormalizeTitles(ai.TitleSuggestions, local.TitleSuggestions),
            NormalizeTags(ai.TagSuggestions, local.TagSuggestions),
            NormalizeMaterials(ai.MaterialSuggestions, local.MaterialSuggestions),
            string.IsNullOrWhiteSpace(ai.DescriptionDraft) ? local.DescriptionDraft : ai.DescriptionDraft.Trim(),
            local.MissingTerms,
            NormalizeList(ai.RiskWarnings, local.RiskWarnings),
            local.ActionChecklist.Concat(["Gemini onerisi yayinlanmadan once marka/telif ve Etsy politika kontrolunden gecir."]).Distinct().ToList(),
            ExecutedProvider: "Gemini",
            ExecutedModel: settings.GeminiModel,
            IsFallback: false);
    }

    private async Task<ListingOptimizationResult> OptimizeWithProviderAsync(
        string providerName,
        string modelName,
        bool allowSilentFallback,
        ListingOptimizationInput input,
        Func<string, string, CancellationToken, Task<string>> callApi,
        CancellationToken cancellationToken)
    {
        var local = localOptimizer.Optimize(input);
        try
        {
            var system = ListingDraftInstructionBuilder.BuildSystemInstruction();
            var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);
            var rawText = await callApi(system, prompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(rawText) || rawText.StartsWith("⚠️"))
            {
                var err = rawText ?? $"{providerName} ({modelName}) yanıt döndürmedi.";
                if (!allowSilentFallback)
                {
                    throw new InvalidOperationException(err);
                }
                return CreateLocalFallbackResult(local, err, providerName, modelName);
            }

            var outputText = StripJsonFences(rawText);
            var ai = JsonSerializer.Deserialize<AiListingOptimizationResponse>(
                outputText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (ai is null)
            {
                var err = $"{providerName} ({modelName}) yanıtı JSON olarak okunamadı.";
                if (!allowSilentFallback)
                {
                    throw new InvalidOperationException(err);
                }
                return CreateLocalFallbackResult(local, err, providerName, modelName);
            }

            return new ListingOptimizationResult(
                local.CurrentSeoScore,
                Math.Max(local.OptimizedSeoScore, Math.Min(100, local.CurrentSeoScore + 12)),
                NormalizeTitles(ai.TitleSuggestions, local.TitleSuggestions),
                NormalizeTags(ai.TagSuggestions, local.TagSuggestions),
                NormalizeMaterials(ai.MaterialSuggestions, local.MaterialSuggestions),
                string.IsNullOrWhiteSpace(ai.DescriptionDraft) ? local.DescriptionDraft : ai.DescriptionDraft.Trim(),
                local.MissingTerms,
                NormalizeList(ai.RiskWarnings, local.RiskWarnings),
                local.ActionChecklist.Concat([$"{providerName} önerisi yayınlanmadan önce marka/telif ve Etsy politika kontrolünden geçir."]).Distinct().ToList(),
                ExecutedProvider: providerName,
                ExecutedModel: modelName,
                IsFallback: false);
        }
        catch (Exception ex)
        {
            if (!allowSilentFallback)
            {
                throw new InvalidOperationException($"Seçtiğiniz {providerName} modeli ({modelName}) yanıt veremedi: {ex.Message}\n\nLütfen tekrar deneyin veya başka bir model seçin.");
            }
            return CreateLocalFallbackResult(local, ex.Message, providerName, modelName);
        }
    }

    private static ListingOptimizationResult CreateLocalFallbackResult(
        ListingOptimizationResult local,
        string reason,
        string providerName = "Gemini",
        string modelName = "")
    {
        var modelInfo = string.IsNullOrWhiteSpace(modelName) ? providerName : $"{providerName} ({modelName})";
        var warning = $"{modelInfo} kullanilamadi; offline kural motoru onerileri gosterildi. Neden: {reason}";
        return local with
        {
            RiskWarnings = local.RiskWarnings.Concat([warning]).Distinct().ToList(),
            ActionChecklist = local.ActionChecklist.Concat([
                $"{modelInfo} gecici olarak yanit veremedigi icin sonuc offline kural motorundan aninda uretildi.",
                "AI Ayarlari ekraninda baska bir model (ornegin gemini-1.5-flash veya gpt-4o) secebilirsiniz.",
            ]).Distinct().ToList(),
            ExecutedProvider = "Offline",
            ExecutedModel = "Kural Tabanlı (Fallback)",
            IsFallback = true,
            FallbackReason = $"{modelInfo} yanıt veremedi: {reason}"
        };
    }

    private static async Task<string> SendGeminiRequestAsync(
        AiOptimizationSettings settings,
        ListingOptimizationInput input,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        string lastBody = "";
        HttpStatusCode lastStatusCode = 0;
        string currentModel = AiModelNormalizer.NormalizeGeminiTextModel(settings.GeminiModel);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:generateContent?key={settings.GeminiApiKey.Trim()}";
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(CreateGeminiPayload(input, currentModel)),
                    Encoding.UTF8,
                    "application/json");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(25)); // 25s per call

                using var response = await HttpClient.SendAsync(request, cts.Token);
                lastStatusCode = response.StatusCode;
                lastBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return lastBody;
                }

                if (lastStatusCode == HttpStatusCode.NotFound && currentModel != "gemini-1.5-flash")
                {
                    currentModel = "gemini-1.5-flash";
                    continue;
                }

                if (!IsTransientGeminiStatus(response.StatusCode) || attempt == maxAttempts)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout on 3.7 -> instantly fallback to 1.5-flash
                if (currentModel != "gemini-1.5-flash")
                {
                    currentModel = "gemini-1.5-flash";
                    continue;
                }
                break;
            }
            catch when (attempt < maxAttempts)
            {
                if (currentModel != "gemini-1.5-flash")
                {
                    currentModel = "gemini-1.5-flash";
                    continue;
                }
            }

            var delay = TimeSpan.FromSeconds(attempt * 1.5);
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException(CreateGeminiErrorMessage(lastStatusCode, lastBody));
    }

    private static object CreateOpenAiPayload(string model, ListingOptimizationInput input) => new
    {
        model = AiModelNormalizer.NormalizeOpenAiTextModel(model),
        instructions = ListingDraftInstructionBuilder.BuildSystemInstruction(),
        input = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input),
    };

    private static object CreateGeminiPayload(ListingOptimizationInput input, string model)
    {
        if (model.Contains("3.7") || model.Contains("3-7"))
        {
            return new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = ListingDraftInstructionBuilder.BuildSystemInstruction() } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input) } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.65,
                    response_mime_type = "application/json",
                    thinking_config = new
                    {
                        thinking_level = "low"
                    }
                }
            };
        }

        return new
        {
            system_instruction = new
            {
                parts = new[] { new { text = ListingDraftInstructionBuilder.BuildSystemInstruction() } }
            },
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input) } }
                }
            },
            generationConfig = new
            {
                temperature = 0.65,
                response_mime_type = "application/json"
            }
        };
    }

    private static bool IsTransientGeminiStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static string CreateGeminiErrorMessage(HttpStatusCode statusCode, string body)
    {
        if (IsGeminiOverloaded(body))
        {
            return "Gemini modeli şu anda yoğun. Program 3 kez otomatik denedi ama Google yine yoğunluk cevabı verdi. Biraz sonra tekrar deneyin veya geçici olarak AI Ayarları > Offline modunu kullanın.";
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return "Gemini kota veya hız limitine takıldı. Biraz bekleyip tekrar deneyin ya da Google AI Studio kota/limit ayarlarınızı kontrol edin.";
        }

        if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
        {
            return "Gemini API key kabul edilmedi. AI Ayarları ekranındaki Gemini key değerini ve Google AI Studio API izinlerini kontrol edin.";
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return "Gemini modeli bulunamadı (HTTP 404). AI Ayarları ekranında 'gemini-2.5-flash' veya 'gemini-2.0-flash' modelini seçtiğinizden emin olun.";
        }

        return $"Gemini isteği başarısız. HTTP {(int)statusCode}: {body}";
    }

    private static bool IsGeminiOverloaded(string body) =>
        body.Contains("overloaded", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("demand", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("try again later", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoverableGeminiException(InvalidOperationException exception) =>
        exception.Message.Contains("Gemini modeli şu anda yoğun", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Gemini kota veya hız limitine takıldı", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Gemini modeli bulunamadı", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("HTTP 500", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("HTTP 502", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("HTTP 503", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("HTTP 504", StringComparison.OrdinalIgnoreCase);

    private static string ExtractOutputText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString() ?? "";
        }

        if (document.RootElement.TryGetProperty("output", out var output))
        {
            foreach (var outputItem in output.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out var content)) continue;
                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "";
                    }
                }
            }
        }

        throw new InvalidOperationException("OpenAI yanitinda output_text bulunamadi.");
    }

    private static string ExtractGeminiOutputText(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                if (parts[0].TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "";
                }
            }
        }

        if (document.RootElement.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString() ?? "";
        }

        if (document.RootElement.TryGetProperty("steps", out var steps))
        {
            foreach (var step in steps.EnumerateArray())
            {
                if (!step.TryGetProperty("content", out var content)) continue;
                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "";
                    }
                }
            }
        }

        throw new InvalidOperationException("Gemini yanitinda metin bulunamadi.");
    }

    private static string StripJsonFences(string value)
    {
        var text = value.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = text.IndexOf('\n');
            if (firstLineEnd >= 0) text = text[(firstLineEnd + 1)..];
            var fenceIndex = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceIndex >= 0) text = text[..fenceIndex].Trim();
        }

        var startIdx = text.IndexOf('{');
        var endIdx = text.LastIndexOf('}');
        if (startIdx >= 0 && endIdx > startIdx)
        {
            return text[startIdx..(endIdx + 1)].Trim();
        }

        return text;
    }

    private static IReadOnlyList<string> NormalizeTitles(
        IReadOnlyList<string>? values,
        IReadOnlyList<string> fallback) =>
        NormalizeList(values, fallback)
            .Select(value => value.Length <= 140 ? value : value[..140].TrimEnd())
            .Take(3)
            .ToList();

    private static IReadOnlyList<string> NormalizeTags(
        IReadOnlyList<string>? values,
        IReadOnlyList<string> fallback) =>
        NormalizeList(values, fallback)
            .Select(value => value.Length <= 20 ? value : value[..20].TrimEnd())
            .Where(value => value.Length >= 2)
            .Take(13)
            .ToList();

    private static IReadOnlyList<string> NormalizeMaterials(
        IReadOnlyList<string>? values,
        IReadOnlyList<string> fallback) =>
        NormalizeList(values, fallback)
            .Select(value => value.Length <= 45 ? value : value[..45].TrimEnd())
            .Where(value => value.Length >= 2)
            .Take(13)
            .ToList();

    private static IReadOnlyList<string> NormalizeList(
        IReadOnlyList<string>? values,
        IReadOnlyList<string> fallback)
    {
        var list = values?
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return list is { Count: > 0 } ? list : fallback;
    }

    private sealed class AiListingOptimizationResponse
    {
        [JsonPropertyName("title_suggestions")]
        public List<string> TitleSuggestions { get; set; } = [];

        [JsonPropertyName("tag_suggestions")]
        public List<string> TagSuggestions { get; set; } = [];

        [JsonPropertyName("material_suggestions")]
        public List<string> MaterialSuggestions { get; set; } = [];

        [JsonPropertyName("description_draft")]
        public string DescriptionDraft { get; set; } = "";

        [JsonPropertyName("risk_warnings")]
        public List<string> RiskWarnings { get; set; } = [];
    }
}
