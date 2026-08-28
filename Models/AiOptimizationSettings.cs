namespace SimilarProductsWinForms.Models;

internal sealed class AiOptimizationSettings
{
    public string Provider { get; set; } = "Offline";
    public string OpenAiApiKey { get; set; } = "";
    public string OpenAiModel { get; set; } = "gpt-5.5";
    public string OpenAiImageModel { get; set; } = "gpt-image-1";
    public string GeminiApiKey { get; set; } = "";
    public string GeminiModel { get; set; } = "gemini-3.7-flash";
    public string GeminiImageModel { get; set; } = "gemini-3.1-flash-image";
    public string ClaudeApiKey { get; set; } = "";
    public string ClaudeModel { get; set; } = "claude-3-7-sonnet";
    public string PlatformToken { get; set; } = "";

    public bool UseOpenAi =>
        Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(OpenAiApiKey);

    public bool UseGemini =>
        Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(GeminiApiKey);

    public bool IsOffline => Provider.Equals("Offline", StringComparison.OrdinalIgnoreCase);

    public string GetActiveBadgeText()
    {
        if (UseOpenAi) return $"🟢 Aktif: OpenAI ({OpenAiModel})";
        if (UseGemini) return $"🔵 Aktif: Gemini ({GeminiModel})";
        return "⚡ Aktif: Offline Kural Motoru";
    }

    public string GetActiveEngineName()
    {
        if (UseOpenAi) return $"OpenAI ({OpenAiModel})";
        if (UseGemini) return $"Google Gemini ({GeminiModel})";
        return "Offline Yerel Kural Motoru";
    }
}
