namespace SimilarProductsWinForms.Models;

internal sealed class AiOptimizationSettings
{
    public string Provider { get; set; } = "Offline";
    public string OpenAiApiKey { get; set; } = "";
    public string OpenAiModel { get; set; } = "gpt-5.5";

    public bool UseOpenAi =>
        Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(OpenAiApiKey);
}
