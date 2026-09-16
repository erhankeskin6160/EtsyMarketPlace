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
                var modelName = AiModelNormalizer.NormalizeGeminiTextModel(settings.GeminiModel);
                providerUsed = $"Google Gemini ({modelName})";
                rawJson = validImages.Count > 0
                    ? await CallGeminiVisionAsync(settings, title, validImages[0], description, tags, cancellationToken)
                    : await CallGeminiTextAsync(settings, title, description, tags, cancellationToken);
            }
            else if (settings.UseOpenAi)
            {
                var modelName = AiModelNormalizer.NormalizeOpenAiTextModel(settings.OpenAiModel);
                providerUsed = $"OpenAI ({modelName})";
                rawJson = validImages.Count > 0
                    ? await CallOpenAiVisionAsync(settings, title, validImages[0], description, tags, cancellationToken)
                    : await CallOpenAiTextAsync(settings, title, description, tags, cancellationToken);
            }
            else if (settings.UseClaude)
            {
                var modelName = AiModelNormalizer.NormalizeClaudeTextModel(settings.ClaudeModel);
                providerUsed = $"Claude ({modelName})";
                rawJson = validImages.Count > 0
                    ? await CallClaudeVisionAsync(settings, title, validImages[0], description, tags, cancellationToken)
                    : await CallClaudeTextAsync(settings, title, description, tags, cancellationToken);
            }
            else if (settings.UseDeepSeek)
            {
                var modelName = AiModelNormalizer.NormalizeDeepSeekModel(settings.DeepSeekModel);
                providerUsed = $"DeepSeek ({modelName})";
                rawJson = await AiProviderCaller.CallDeepSeekAsync(
                    BuildSystemPrompt(),
                    BuildUserPrompt(title, description, tags, hasImage: false),
                    settings.DeepSeekApiKey,
                    modelName,
                    maxTokens: 500,
                    temperature: 0.2,
                    ct: cancellationToken);
            }
            else if (settings.UseGrok)
            {
                var modelName = AiModelNormalizer.NormalizeGrokModel(settings.GrokModel);
                providerUsed = $"xAI Grok ({modelName})";
                rawJson = await AiProviderCaller.CallGrokAsync(
                    BuildSystemPrompt(),
                    BuildUserPrompt(title, description, tags, hasImage: false),
                    settings.GrokApiKey,
                    modelName,
                    maxTokens: 500,
                    temperature: 0.2,
                    ct: cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                var parsed = ParseCategoryResponse(rawJson, providerUsed);
                if (parsed != null && parsed.TaxonomyId > 0)
                {
                    parsed.ProviderUsed = providerUsed;
                    return parsed;
                }
            }
        }
        catch
        {
            // Servis veya bağlantı hatasında çevrimdışı fallback motor devreye girer
        }

        // AI yanıt veremezse güvenilir kural tabanlı yerel motor ile dön ve açıkça belirt
        var fallback = LocalCategoryHeuristics.SuggestFromText(title, description, tags);
        fallback.ProviderUsed = "Yerel Kural Motoru (Çevrimdışı Fallback)";
        return fallback;
    }

    private static string BuildSystemPrompt() =>
        "You are an expert Etsy Taxonomy and Category Specialist with deep knowledge of the entire Etsy taxonomy tree across ALL departments " +
        "(Bags & Purses, Clothing & Shoes, Home & Living, Jewelry & Accessories, Art & Collectibles, Craft Supplies, Electronics & Accessories, etc.).\n\n" +
        "CRITICAL INSTRUCTIONS:\n" +
        "1. Never restrict yourself to any specific niche or material. You have full freedom to select ANY category on Etsy that best matches the item.\n" +
        "2. Analyze the product title, image (if provided), description, and tags to understand what the item ACTUALLY is physically and functionally.\n" +
        "   - E.g. If the title/image is a women's handbag, shoulder bag, or purse ('El yapımı kadın çantası'), categorize it under 'Bags & Purses > Handbags > Shoulder Bags' (Taxonomy ID: 132) or the most specific bag category.\n" +
        "   - E.g. If it is clothing, jewelry, home decor, or digital art, choose the exact leaf category corresponding to that item.\n" +
        "3. Select the most specific leaf category that provides maximum SEO visibility, organic discovery, and conversion rate for Etsy shoppers.\n" +
        "4. In the 'reasoning' field, explain your decision referencing the title, image features, or description details.\n\n" +
        "You MUST respond ONLY with a single valid raw JSON object (no markdown, no backticks, no text before or after) matching this schema:\n" +
        "{\n" +
        "  \"taxonomy_id\": <number>,\n" +
        "  \"category_path\": \"<Department > Subcategory > Specific Leaf>\",\n" +
        "  \"confidence_score\": <number between 1 and 100>,\n" +
        "  \"reasoning\": \"<Detailed reasoning referencing title, image, and description>\",\n" +
        "  \"alternatives\": [\n" +
        "    {\"taxonomy_id\": <number>, \"category_path\": \"<Path>\", \"confidence_score\": <number>}\n" +
        "  ]\n" +
        "}";

    private static string BuildUserPrompt(string title, string? desc, string? tags, bool hasImage) =>
        $"Product Title: {title}\n" +
        (string.IsNullOrWhiteSpace(desc) ? "" : $"Product Description: {desc.Substring(0, Math.Min(desc.Length, 600))}\n") +
        (string.IsNullOrWhiteSpace(tags) ? "" : $"Tags: {tags}\n") +
        (hasImage
            ? "A commercial photo of the physical product is provided. Inspect the visual shape, material, and purpose together with the title and description to select the most accurate Etsy taxonomy category."
            : "Analyze the product title, description, and keywords to determine the exact Etsy category and taxonomy ID.");

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
