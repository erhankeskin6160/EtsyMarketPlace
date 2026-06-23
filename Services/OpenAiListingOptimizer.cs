namespace SimilarProductsWinForms.Services;

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
        if (!settings.UseOpenAi)
        {
            return localOptimizer.Optimize(input);
        }

        var local = localOptimizer.Optimize(input);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAiApiKey.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(CreatePayload(settings.OpenAiModel, input)),
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
            string.IsNullOrWhiteSpace(ai.DescriptionDraft) ? local.DescriptionDraft : ai.DescriptionDraft.Trim(),
            local.MissingTerms,
            NormalizeList(ai.RiskWarnings, local.RiskWarnings),
            local.ActionChecklist.Concat(["AI onerisi yayinlanmadan once marka/telif ve Etsy politika kontrolunden gecir."]).Distinct().ToList());
    }

    private static object CreatePayload(string model, ListingOptimizationInput input) => new
    {
        model = string.IsNullOrWhiteSpace(model) ? "gpt-5.5" : model.Trim(),
        input =
            "You are an Etsy SEO listing optimization assistant. " +
            "Return only valid JSON with keys title_suggestions, tag_suggestions, description_draft, risk_warnings. " +
            "Rules: title_suggestions must contain 3 titles under 140 characters; tag_suggestions must contain up to 13 Etsy tags, each 20 characters or less; " +
            "description_draft must be buyer-facing Turkish text; risk_warnings must flag trademark/copyright risks. " +
            $"Target keyword: {input.TargetKeyword}\n" +
            $"Current title: {input.Title}\n" +
            $"Current tags: {string.Join(", ", input.Tags)}\n" +
            $"Current description: {input.Description}",
    };

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

        [JsonPropertyName("description_draft")]
        public string DescriptionDraft { get; set; } = "";

        [JsonPropertyName("risk_warnings")]
        public List<string> RiskWarnings { get; set; } = [];
    }
}
