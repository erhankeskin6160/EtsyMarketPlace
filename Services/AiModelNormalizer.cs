namespace SimilarProductsWinForms.Services;

using System;

internal static class AiModelNormalizer
{
    public static string NormalizeGeminiTextModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gemini-3.7-flash";

        var clean = model.Trim().ToLowerInvariant();

        // 3.x Series
        if (clean.Contains("3.7") || clean.Contains("3-7")) return "gemini-3.7-flash";
        if (clean.Contains("3.6") || clean.Contains("3-6")) return "gemini-3.6-flash";
        if (clean.Contains("3.5") || clean.Contains("3-5")) return "gemini-3.5-flash";
        if (clean.Contains("3.1-pro") || clean.Contains("3.1 pro") || clean.Contains("3-1-pro")) return "gemini-3.1-pro-preview";

        // 2.x Series
        if (clean.Contains("2.5-pro") || clean.Contains("2.5 pro")) return "gemini-2.5-pro";
        if (clean.Contains("thinking")) return "gemini-2.0-flash-thinking-exp";
        if (clean.Contains("2.0-pro") || clean.Contains("2.0 pro")) return "gemini-2.0-pro-exp-02-05";
        if (clean.Contains("2.0") || clean.Contains("2-0")) return "gemini-2.0-flash";

        // 1.5 Series
        if (clean.Contains("1.5-pro") || clean.Contains("pro")) return "gemini-1.5-pro";
        if (clean.Contains("8b")) return "gemini-1.5-flash-8b";
        if (clean.Contains("1.5-flash") || clean.Contains("1.5 flash")) return "gemini-1.5-flash";

        if (clean.StartsWith("gemini-")) return clean;

        return "gemini-3.7-flash";
    }

    public static string NormalizeGeminiImageModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gemini-3.1-flash-image";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("banana") || clean.Contains("3.1") || clean.Contains("3-1") || clean.Contains("flash-image") || clean == "gemini-image")
        {
            return "gemini-3.1-flash-image";
        }

        if (clean.Contains("2.5") || clean.Contains("2-5"))
        {
            return "gemini-2.5-flash-image";
        }

        if (clean.Contains("imagen-3") || clean.Contains("imagen 3"))
        {
            return "imagen-3.0-generate-002";
        }

        if (clean.StartsWith("gemini-"))
        {
            return clean;
        }

        return "gemini-3.1-flash-image";
    }

    public static string NormalizeOpenAiTextModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gpt-4o";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("o3-mini") || clean.Contains("o3")) return "o3-mini";
        if (clean == "o1" || clean.Contains("o1-preview") || clean.Contains("o1-mini")) return clean;
        if (clean.Contains("mini")) return "gpt-4o-mini";
        if (clean.Contains("4o")) return "gpt-4o";
        if (clean.Contains("5.5") || clean.Contains("gpt-5")) return "gpt-4o";
        if (clean.Contains("turbo")) return "gpt-4-turbo";

        return clean.StartsWith("gpt-") || clean.StartsWith("o") ? clean : "gpt-4o";
    }

    public static string NormalizeOpenAiImageModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "dall-e-3";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("dall-e-2") || clean.Contains("dalle-2")) return "dall-e-2";
        if (clean.Contains("dall-e-3") || clean.Contains("dalle-3") || clean.Contains("gpt-image")) return "dall-e-3";

        return "dall-e-3";
    }

    public static string NormalizeClaudeTextModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "claude-3-7-sonnet-20250219";

        var clean = model.Trim().ToLowerInvariant();
        if (clean.Contains("3-7") || clean.Contains("3.7")) return "claude-3-7-sonnet-20250219";
        if (clean.Contains("haiku")) return "claude-3-5-haiku-20241022";
        if (clean.Contains("3-5") || clean.Contains("3.5")) return "claude-3-5-sonnet-20241022";

        return clean.StartsWith("claude-") ? clean : "claude-3-7-sonnet-20250219";
    }
}
