namespace SimilarProductsWinForms.Services;

using System.Text.Json;
using SimilarProductsWinForms.Models;

internal static class AiOptimizationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "ai-optimization-settings.json");
        }
    }

    public static AiOptimizationSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new AiOptimizationSettings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AiOptimizationSettings>(json) ?? new AiOptimizationSettings();

            bool needsSave = false;
            // Gemini model adı doğrulama / 404 önleme
            if (string.IsNullOrWhiteSpace(settings.GeminiModel) ||
                settings.GeminiModel.Contains("3.7") ||
                settings.GeminiModel.Contains("3.8") ||
                settings.GeminiModel.Contains("3.5"))
            {
                settings.GeminiModel = "gemini-2.5-flash";
                needsSave = true;
            }

            // OpenAI model adı doğrulama
            if (string.IsNullOrWhiteSpace(settings.OpenAiModel) ||
                settings.OpenAiModel.Contains("5.5") ||
                settings.OpenAiModel.Contains("5.4") ||
                settings.OpenAiModel.Contains("Astra"))
            {
                settings.OpenAiModel = "gpt-4o";
                needsSave = true;
            }

            if (needsSave)
            {
                Save(settings);
            }

            return settings;
        }
        catch
        {
            return new AiOptimizationSettings();
        }
    }

    public static void Save(AiOptimizationSettings settings)
    {
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
