namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;

internal sealed class AiCategorySuggester : IAiCategorySuggester
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Func<AiOptimizationSettings> _loadSettings;

    public AiCategorySuggester(Func<AiOptimizationSettings>? loadSettings = null)
    {
        _loadSettings = loadSettings ?? AiOptimizationSettingsStore.Load;
    }

    public async Task<CategorySuggestionResult> SuggestCategoryAsync(
        string title,
        IReadOnlyList<string> imagePaths,
        string? description = null,
        string? tags = null,
        CancellationToken cancellationToken = default)
    {
        var settings = _loadSettings();
        var validImages = imagePaths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).ToList();

        if (settings.IsOffline)
        {
            return LocalCategoryHeuristics.SuggestFromText(title, description, tags);
        }

        try
        {
            string? rawJson = null;
            string providerUsed = settings.Provider;

            if (settings.UseGemini)
            {
                providerUsed = "Gemini";
                rawJson = validImages.Count > 0
                    ? await CallGeminiVisionAsync(settings, title, validImages[0], description, tags, cancellationToken)
                    : await CallGeminiTextAsync(settings, title, description, tags, cancellationToken);
            }
            else if (settings.UseOpenAi)
            {
                providerUsed = "OpenAI";
                rawJson = validImages.Count > 0
                    ? await CallOpenAiVisionAsync(settings, title, validImages[0], description, tags, cancellationToken)
                    : await CallOpenAiTextAsync(settings, title, description, tags, cancellationToken);
            }
            else if (settings.UseClaude)
            {
                providerUsed = "Claude";
                rawJson = validImages.Count > 0
                    ? await CallClaudeVisionAsync(settings, title, validImages[0], description, tags, cancellationToken)
                    : await CallClaudeTextAsync(settings, title, description, tags, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                var parsed = ParseCategoryResponse(rawJson, providerUsed);
                if (parsed != null && parsed.TaxonomyId > 0)
                {
                    return parsed;
                }
            }
        }
        catch
        {
            // Servis veya bağlantı hatasında çevrimdışı motor devreye girer
        }

        // AI yanıt veremezse güvenilir kural tabanlı yerel motor ile dön
        return LocalCategoryHeuristics.SuggestFromText(title, description, tags);
    }

    private static string BuildSystemPrompt() =>
        "You are an expert Etsy Taxonomy and Category Specialist. Your task is to analyze the product title and visual image, " +
        "then determine the exact Etsy Category (Taxonomy ID and Category Path) where this product will get the best SEO visibility and conversion. " +
        "You MUST respond ONLY with a valid raw JSON object (no markdown, no backticks, no extra text) with this exact schema:\n" +
        "{\n" +
        "  \"taxonomy_id\": 2079,\n" +
        "  \"category_path\": \"Electronics & Accessories > Audio > Headphone & Headset Stands\",\n" +
        "  \"confidence_score\": 95,\n" +
        "  \"reasoning\": \"Resimde kulaklık tutucu stand olarak tasarlanmış 3D figür görülüyor.\",\n" +
        "  \"alternatives\": [\n" +
        "    {\"taxonomy_id\": 1239, \"category_path\": \"Art & Collectibles > Sculptures > Busts & Statues\", \"confidence_score\": 85},\n" +
        "    {\"taxonomy_id\": 6701, \"category_path\": \"Home & Living > Office & School Supplies > Desk Accessories\", \"confidence_score\": 75}\n" +
        "  ]\n" +
        "}";

    private static string BuildUserPrompt(string title, string? desc, string? tags, bool hasImage) =>
        $"Product Title: {title}\n" +
        (string.IsNullOrWhiteSpace(desc) ? "" : $"Description snippet: {desc.Substring(0, Math.Min(desc.Length, 200))}\n") +
        (string.IsNullOrWhiteSpace(tags) ? "" : $"Tags: {tags}\n") +
        (hasImage ? "A commercial photo of the physical item is attached. Pay close attention to what the item actually is physically (e.g. headphone stand, statue, mug, jewelry, decor)." : "Analyze the title and context to determine the best Etsy category.");

    // --- OpenAI ---

    private static async Task<string> CallOpenAiVisionAsync(
        AiOptimizationSettings settings, string title, string imagePath,
        string? desc, string? tags, CancellationToken ct)
    {
        byte[] bytes = await File.ReadAllBytesAsync(imagePath, ct);
        string mime = GetMimeType(imagePath);
        string base64Url = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

        var payload = new
        {
            model = "gpt-4o",
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt() },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = BuildUserPrompt(title, desc, tags, hasImage: true) },
                        new { type = "image_url", image_url = new { url = base64Url, detail = "low" } }
                    }
                }
            },
            max_tokens = 500,
            temperature = 0.2
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAiApiKey.Trim());
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return string.Empty;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private static async Task<string> CallOpenAiTextAsync(
        AiOptimizationSettings settings, string title,
        string? desc, string? tags, CancellationToken ct)
    {
        return await AiProviderCaller.CallOpenAiAsync(
            BuildSystemPrompt(),
            BuildUserPrompt(title, desc, tags, hasImage: false),
            settings.OpenAiApiKey,
            settings.OpenAiModel,
            maxTokens: 500,
            temperature: 0.2,
            ct: ct);
    }

    // --- Gemini ---

    private static async Task<string> CallGeminiVisionAsync(
        AiOptimizationSettings settings, string title, string imagePath,
        string? desc, string? tags, CancellationToken ct)
    {
        byte[] bytes = await File.ReadAllBytesAsync(imagePath, ct);
        string mime = GetMimeType(imagePath);
        string model = AiModelNormalizer.NormalizeGeminiTextModel(settings.GeminiModel);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(settings.GeminiApiKey.Trim())}";

        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = BuildSystemPrompt() } } },
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = BuildUserPrompt(title, desc, tags, hasImage: true) },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mime,
                                data = Convert.ToBase64String(bytes)
                            }
                        }
                    }
                }
            },
            generationConfig = new { maxOutputTokens = 500, temperature = 0.2 }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return string.Empty;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
        {
            var parts = cands[0].GetProperty("content").GetProperty("parts");
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var t)) return t.GetString() ?? "";
            }
        }
        return string.Empty;
    }

    private static async Task<string> CallGeminiTextAsync(
        AiOptimizationSettings settings, string title,
        string? desc, string? tags, CancellationToken ct)
    {
        return await AiProviderCaller.CallGeminiAsync(
            BuildSystemPrompt(),
            BuildUserPrompt(title, desc, tags, hasImage: false),
            settings.GeminiApiKey,
            settings.GeminiModel,
            maxTokens: 500,
            temperature: 0.2,
            ct: ct);
    }

    // --- Claude ---

    private static async Task<string> CallClaudeVisionAsync(
        AiOptimizationSettings settings, string title, string imagePath,
        string? desc, string? tags, CancellationToken ct)
    {
        byte[] bytes = await File.ReadAllBytesAsync(imagePath, ct);
        string mime = GetMimeType(imagePath);
        string model = AiModelNormalizer.NormalizeClaudeTextModel(settings.ClaudeModel);

        var payload = new
        {
            model,
            max_tokens = 500,
            temperature = 0.2,
            system = BuildSystemPrompt(),
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mime,
                                data = Convert.ToBase64String(bytes)
                            }
                        },
                        new { type = "text", text = BuildUserPrompt(title, desc, tags, hasImage: true) }
                    }
                }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", settings.ClaudeApiKey.Trim());
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await HttpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return string.Empty;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("content", out var arr) && arr.GetArrayLength() > 0)
        {
            return arr[0].GetProperty("text").GetString() ?? "";
        }
        return string.Empty;
    }

    private static async Task<string> CallClaudeTextAsync(
        AiOptimizationSettings settings, string title,
        string? desc, string? tags, CancellationToken ct)
    {
        return await AiProviderCaller.CallClaudeAsync(
            BuildSystemPrompt(),
            BuildUserPrompt(title, desc, tags, hasImage: false),
            settings.ClaudeApiKey,
            settings.ClaudeModel,
            maxTokens: 500,
            temperature: 0.2,
            ct: ct);
    }

    // --- Parsing & Utilities ---

    public static CategorySuggestionResult? ParseCategoryResponse(string rawText, string provider) =>
        CategoryResponseParser.Parse(rawText, provider);

    private static string StripJsonFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(7);
        }
        else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(3);
        }

        if (trimmed.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }

        return trimmed.Trim();
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }
}
