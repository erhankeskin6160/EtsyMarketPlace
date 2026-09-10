namespace SimilarProductsWinForms.Services;

using System;

internal static class AiModelNormalizer
{
    public static string NormalizeGeminiTextModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gemini-2.5-flash";

        var clean = model.Trim().ToLowerInvariant();

        // 3.x Series migration (Gemini has no 3.x public API; map to 2.5 flash to prevent 404)
        if (clean.Contains("3.8") || clean.Contains("3-8") ||
            clean.Contains("3.7") || clean.Contains("3-7") ||
            clean.Contains("3.5") || clean.Contains("3-5"))
            return "gemini-2.5-flash";

        // 2.5 Series (Official Google AI Studio models)
        if (clean.Contains("2.5-pro") || clean.Contains("2.5 pro")) return "gemini-2.5-pro";
        if (clean.Contains("2.5-flash") || clean.Contains("2.5 flash") || clean.Contains("2.5")) return "gemini-2.5-flash";

        // 2.0 Series
        if (clean.Contains("thinking")) return "gemini-2.0-flash-thinking-exp";
        if (clean.Contains("2.0-pro") || clean.Contains("2.0 pro")) return "gemini-2.0-pro-exp-02-05";
        if (clean.Contains("lite")) return "gemini-2.0-flash-lite";
        if (clean.Contains("2.0") || clean.Contains("2-0")) return "gemini-2.0-flash";

        // 1.5 Series
        if (clean.Contains("1.5-pro") || clean.Contains("1.5 pro")) return "gemini-1.5-pro";
        if (clean.Contains("8b")) return "gemini-1.5-flash-8b";
        if (clean.Contains("1.5-flash") || clean.Contains("1.5 flash") || clean.Contains("1.5")) return "gemini-1.5-flash";

        if (clean.StartsWith("gemini-")) return clean;

        return "gemini-2.5-flash";
    }

    public static string NormalizeGeminiImageModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gemini-2.5-flash-image";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("imagen-3") || clean.Contains("imagen 3")) return "imagen-3.0-generate-002";
        if (clean.Contains("2.5") || clean.Contains("2-5")) return "gemini-2.5-flash-image";
        if (clean.Contains("2.0") || clean.Contains("2-0")) return "gemini-2.0-flash";

        if (clean.StartsWith("gemini-") || clean.StartsWith("imagen-")) return clean;

        return "gemini-2.5-flash-image";
    }

    public static string NormalizeOpenAiTextModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gpt-4o";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("astra") || clean.Contains("gpt-6") || clean.Contains("5.5") || clean.Contains("5.4")) return "gpt-4o";
        if (clean.Contains("o4-mini") || clean.Contains("o4")) return "o3-mini";
        if (clean.Contains("o3-mini") || clean.Contains("o3 mini")) return "o3-mini";
        if (clean.Contains("o3-pro") || clean.Contains("o3 pro")) return "o3-mini";
        if (clean == "o3") return "o3-mini";
        if (clean.Contains("o1-mini") || clean.Contains("o1 mini")) return "o1-mini";
        if (clean.Contains("o1-preview") || clean == "o1" || clean.Contains("o1")) return "o1";
        if (clean.Contains("chatgpt-4o-latest") || clean.Contains("latest")) return "chatgpt-4o-latest";
        if (clean.Contains("4o-mini") || clean.Contains("4o mini")) return "gpt-4o-mini";
        if (clean.Contains("4o")) return "gpt-4o";
        if (clean.Contains("turbo")) return "gpt-4-turbo";

        return clean.StartsWith("gpt-") || clean.StartsWith("o") ? clean : "gpt-4o";
    }

    public static string NormalizeOpenAiImageModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gpt-image-2.5-flare";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("sunburst")) return "gpt-image-2.5-sunburst";
        if (clean.Contains("flare")) return "gpt-image-2.5-flare";
        if (clean.Contains("gpt-image-2.5")) return "gpt-image-2.5-flare";
        if (clean.Contains("gpt-image-2")) return "gpt-image-2";
        if (clean.Contains("dall-e-3") || clean.Contains("dalle-3") || clean.Contains("dall-e 3")) return "dall-e-3";
        if (clean.Contains("dall-e-2") || clean.Contains("dalle-2")) return "dall-e-2";

        return clean.StartsWith("gpt-image") || clean.StartsWith("dall-e") ? clean : "gpt-image-2.5-flare";
    }

    public static string NormalizeClaudeTextModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "claude-3-7-sonnet-20250219";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("3-7") || clean.Contains("3.7") || clean.Contains("fable") || clean.Contains("sonnet-5")) return "claude-3-7-sonnet-20250219";
        if (clean.Contains("3-5-sonnet") || clean.Contains("3.5-sonnet") || clean.Contains("3.5 sonnet")) return "claude-3-5-sonnet-20241022";
        if (clean.Contains("haiku-3-5") || clean.Contains("3-5-haiku") || clean.Contains("3.5-haiku") || clean.Contains("haiku")) return "claude-3-5-haiku-20241022";
        if (clean.Contains("opus")) return "claude-3-opus-20240229";
        if (clean.Contains("sonnet")) return "claude-3-7-sonnet-20250219";

        return clean.StartsWith("claude-") ? clean : "claude-3-7-sonnet-20250219";
    }

    public static string NormalizeDeepSeekModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "deepseek-reasoner";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("r1") || clean.Contains("reasoner") || clean.Contains("reasoning") || clean.Contains("v4")) return "deepseek-reasoner";
        if (clean.Contains("v3") || clean.Contains("chat")) return "deepseek-chat";

        return clean.StartsWith("deepseek") ? clean : "deepseek-reasoner";
    }

    public static string NormalizeGrokModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "grok-3";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("grok-3") || clean.Contains("grok 3") || clean == "grok3") return "grok-3";
        if (clean.Contains("grok-2") || clean.Contains("grok 2") || clean == "grok2") return "grok-2-latest";
        if (clean.Contains("beta")) return "grok-beta";

        return clean.StartsWith("grok") ? clean : "grok-3";
    }
}
