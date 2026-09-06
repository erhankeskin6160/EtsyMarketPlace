namespace SimilarProductsWinForms.Models;

using System;
using SimilarProductsWinForms.Services;

internal sealed class AiOptimizationSettings
{
    public string Provider { get; set; } = "Offline";
    public string OpenAiApiKey { get; set; } = "";
    public string OpenAiModel { get; set; } = "gpt-4o";
    public string OpenAiImageModel { get; set; } = "dall-e-3";
    public string GeminiApiKey { get; set; } = "";
    public string GeminiModel { get; set; } = "gemini-2.5-flash";
    public string GeminiImageModel { get; set; } = "gemini-2.5-flash-image";
    public string BflApiKey { get; set; } = "";
    public string BflModel { get; set; } = "flux-pro-1.1";
    public string IdeogramApiKey { get; set; } = "";
    public string IdeogramModel { get; set; } = "ideogram-v4";
    public string ClaudeApiKey { get; set; } = "";
    public string ClaudeModel { get; set; } = "claude-3-7-sonnet-20250219";
    public string DeepSeekApiKey { get; set; } = "";
    public string DeepSeekModel { get; set; } = "deepseek-reasoner";
    public string GrokApiKey { get; set; } = "";
    public string GrokModel { get; set; } = "grok-3";
    public string PlatformToken { get; set; } = "";

    public bool UseOpenAi =>
        Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(OpenAiApiKey);

    public bool UseGemini =>
        Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(GeminiApiKey);

    public bool UseClaude =>
        Provider.Equals("Claude", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(ClaudeApiKey);

    public bool UseDeepSeek =>
        Provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(DeepSeekApiKey);

    public bool UseGrok =>
        Provider.Equals("Grok", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(GrokApiKey);

    public bool IsOffline => Provider.Equals("Offline", StringComparison.OrdinalIgnoreCase);

    /// <summary>Herhangi bir AI provider aktif mi?</summary>
    public bool HasAnyAiProvider => UseOpenAi || UseGemini || UseClaude || UseDeepSeek || UseGrok;

    public string GetActiveBadgeText()
    {
        if (UseOpenAi) return $"🟢 Aktif: OpenAI ({AiModelNormalizer.NormalizeOpenAiTextModel(OpenAiModel)})";
        if (UseGemini) return $"🔵 Aktif: Gemini ({AiModelNormalizer.NormalizeGeminiTextModel(GeminiModel)})";
        if (UseClaude) return $"🟣 Aktif: Claude ({AiModelNormalizer.NormalizeClaudeTextModel(ClaudeModel)})";
        if (UseDeepSeek) return $"🔴 Aktif: DeepSeek ({AiModelNormalizer.NormalizeDeepSeekModel(DeepSeekModel)})";
        if (UseGrok) return $"🟠 Aktif: Grok ({AiModelNormalizer.NormalizeGrokModel(GrokModel)})";
        return "⚡ Aktif: Offline Kural Motoru";
    }

    public string GetActiveEngineName()
    {
        if (UseOpenAi) return $"OpenAI ({AiModelNormalizer.NormalizeOpenAiTextModel(OpenAiModel)})";
        if (UseGemini) return $"Google Gemini ({AiModelNormalizer.NormalizeGeminiTextModel(GeminiModel)})";
        if (UseClaude) return $"Anthropic Claude ({AiModelNormalizer.NormalizeClaudeTextModel(ClaudeModel)})";
        if (UseDeepSeek) return $"DeepSeek ({AiModelNormalizer.NormalizeDeepSeekModel(DeepSeekModel)})";
        if (UseGrok) return $"xAI Grok ({AiModelNormalizer.NormalizeGrokModel(GrokModel)})";
        return "Offline Yerel Kural Motoru";
    }
}
