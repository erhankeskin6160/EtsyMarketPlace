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
            return localOptimizer.Optimize(input);
        }

        if (settings.UseGemini)
        {
            return await OptimizeWithGeminiAsync(settings, input, cancellationToken);
        }

        if (settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Gemini API key girilmemis. AI Ayarlari ekraninda Gemini key alanini doldurun.");
        }

        if (!settings.UseOpenAi)
        {
            throw new InvalidOperationException($"{settings.Provider} adapteri henuz aktif degil. Simdilik Offline veya OpenAI kullanin.");
        }

        return await OptimizeWithOpenAiAsync(settings, input, cancellationToken);
    }

    private async Task<ListingOptimizationResult> OptimizeWithOpenAiAsync(
        AiOptimizationSettings settings,
        ListingOptimizationInput input,
        CancellationToken cancellationToken)
    {
        var local = localOptimizer.Optimize(input);
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
            ?? throw new InvalidOperationException("OpenAI yaniti okunamadi.");

        return new ListingOptimizationResult(
            local.CurrentSeoScore,
            Math.Max(local.OptimizedSeoScore, Math.Min(100, local.CurrentSeoScore + 12)),
            NormalizeTitles(ai.TitleSuggestions, local.TitleSuggestions),
            NormalizeTags(ai.TagSuggestions, local.TagSuggestions),
            NormalizeMaterials(ai.MaterialSuggestions, local.MaterialSuggestions),
            string.IsNullOrWhiteSpace(ai.DescriptionDraft) ? local.DescriptionDraft : ai.DescriptionDraft.Trim(),
            local.MissingTerms,
            NormalizeList(ai.RiskWarnings, local.RiskWarnings),
            local.ActionChecklist.Concat(["AI onerisi yayinlanmadan once marka/telif ve Etsy politika kontrolunden gecir."]).Distinct().ToList());
    }

    private async Task<ListingOptimizationResult> OptimizeWithGeminiAsync(
        AiOptimizationSettings settings,
        ListingOptimizationInput input,
        CancellationToken cancellationToken)
    {
        var local = localOptimizer.Optimize(input);
        AiListingOptimizationResponse ai;
        try
        {
            var body = await SendGeminiRequestAsync(settings, input, cancellationToken);
            var outputText = StripJsonFences(ExtractGeminiOutputText(body));
            ai = JsonSerializer.Deserialize<AiListingOptimizationResponse>(
                outputText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Gemini yaniti okunamadi.");
        }
        catch (InvalidOperationException ex) when (IsRecoverableGeminiException(ex))
        {
            return CreateLocalFallbackResult(local, ex.Message);
        }
        catch (JsonException ex)
        {
            return CreateLocalFallbackResult(local, $"Gemini yaniti JSON formatinda okunamadi. Offline oneriler gosterildi. Detay: {ex.Message}");
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
            local.ActionChecklist.Concat(["Gemini onerisi yayinlanmadan once marka/telif ve Etsy politika kontrolunden gecir."]).Distinct().ToList());
    }

    private static ListingOptimizationResult CreateLocalFallbackResult(
        ListingOptimizationResult local,
        string reason)
    {
        var warning = $"Gemini kullanilamadi; offline oneriler gosterildi. Neden: {reason}";
        return local with
        {
            RiskWarnings = local.RiskWarnings.Concat([warning]).Distinct().ToList(),
            ActionChecklist = local.ActionChecklist.Concat([
                "Gemini basarisiz oldugu icin sonuc offline kural motorundan uretildi.",
                "AI Ayarlari ekraninda daha hafif bir Gemini modeli deneyebilir veya bir sure sonra tekrar calistirabilirsiniz.",
            ]).Distinct().ToList(),
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

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions");
            request.Headers.Add("x-goog-api-key", settings.GeminiApiKey.Trim());
            request.Content = new StringContent(
                JsonSerializer.Serialize(CreateGeminiPayload(settings.GeminiModel, input)),
                Encoding.UTF8,
                "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            lastStatusCode = response.StatusCode;
            lastBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return lastBody;
            }

            if (!IsTransientGeminiStatus(response.StatusCode) || attempt == maxAttempts)
            {
                break;
            }

            var delay = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(attempt * 2);
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException(CreateGeminiErrorMessage(lastStatusCode, lastBody));
    }

    private static object CreateOpenAiPayload(string model, ListingOptimizationInput input) => new
    {
        model = string.IsNullOrWhiteSpace(model) ? "gpt-5.5" : model.Trim(),
        input = CreatePrompt(input),
    };

    private static object CreateGeminiPayload(string model, ListingOptimizationInput input) => new
    {
        model = string.IsNullOrWhiteSpace(model) ? "gemini-3.5-flash" : model.Trim(),
        system_instruction = "You are an Etsy SEO listing optimization assistant. Return only valid JSON.",
        input = CreatePrompt(input),
        generation_config = new
        {
            temperature = 0.35,
        },
    };

    private static string CreatePrompt(ListingOptimizationInput input) =>
        "Return only valid JSON with keys title_suggestions, tag_suggestions, material_suggestions, description_draft, risk_warnings. " +
        "Rules: title_suggestions must contain 3 English Etsy titles under 140 characters; tag_suggestions must contain up to 13 English Etsy tags, each 20 characters or less; " +
        "material_suggestions must contain only true physical/digital materials explicitly supported by the current listing text, up to 13 items, each 45 characters or less; " +
        "description_draft must be buyer-facing English text optimized for Etsy SEO and GEO/search intent. " +
        "risk_warnings must be Turkish notes and include Turkish explanations in parentheses when useful. " +
        "Avoid claiming official, licensed, endorsed, or affiliated status unless the current listing explicitly proves it. " +
        $"Target keyword: {input.TargetKeyword}\n" +
        $"Current title: {input.Title}\n" +
        $"Current tags: {string.Join(", ", input.Tags)}\n" +
        $"Current description: {input.Description}";

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
            return "Gemini modeli su anda yogun. Program 3 kez otomatik denedi ama Google yine yogunluk cevabi verdi. Biraz sonra tekrar deneyin veya gecici olarak AI Ayarlari > Offline modunu kullanin.";
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return "Gemini kota veya hiz limitine takildi. Biraz bekleyip tekrar deneyin ya da Google AI Studio kota/limit ayarlarinizi kontrol edin.";
        }

        if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
        {
            return "Gemini API key kabul edilmedi. AI Ayarlari ekranindaki Gemini key degerini ve Google AI Studio API izinlerini kontrol edin.";
        }

        return $"Gemini istegi basarisiz. HTTP {(int)statusCode}: {body}";
    }

    private static bool IsGeminiOverloaded(string body) =>
        body.Contains("overloaded", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("demand", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("try again later", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoverableGeminiException(InvalidOperationException exception) =>
        exception.Message.Contains("Gemini modeli su anda yogun", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Gemini kota veya hiz limitine takildi", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Gemini istegi basarisiz. HTTP 500", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Gemini istegi basarisiz. HTTP 502", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Gemini istegi basarisiz. HTTP 503", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Gemini istegi basarisiz. HTTP 504", StringComparison.OrdinalIgnoreCase);

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

        throw new InvalidOperationException("Gemini yanitinda output_text bulunamadi.");
    }

    private static string StripJsonFences(string value)
    {
        var text = value.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd >= 0) text = text[(firstLineEnd + 1)..];
        var fenceIndex = text.LastIndexOf("```", StringComparison.Ordinal);
        return fenceIndex >= 0 ? text[..fenceIndex].Trim() : text.Trim();
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
