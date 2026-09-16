namespace EtsyMarketPlace.Application.ListingOptimization;

using System;

/// <summary>
/// Normalizes LLM model identifiers (including Gemini 3.8 Flash, Pro, and Claude/GPT variants)
/// ensuring valid API endpoints and consistent configuration throughout the application.
/// </summary>
public static class EtsyAiModelNormalizer
{
    public static string NormalizeGeminiTextModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "gemini-3.8-flash";

        var clean = model.Trim().ToLowerInvariant();

        // 3.8 Series support
        if (clean.Contains("3.8-flash") || clean.Contains("3.8 flash") || clean.Contains("3-8-flash") || clean == "gemini-3.8-flash") return "gemini-3.8-flash";
        if (clean.Contains("3.8-pro") || clean.Contains("3.8 pro") || clean.Contains("3-8-pro")) return "gemini-3.8-pro";
        if (clean.Contains("3.8") || clean.Contains("3-8")) return "gemini-3.8-flash";

        // 2.5 Series
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

        if (clean.StartsWith("gemini-") || !string.IsNullOrWhiteSpace(model)) return model.Trim();

        return "gemini-3.8-flash";
    }
}
