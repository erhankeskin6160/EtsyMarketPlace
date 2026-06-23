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
        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AiOptimizationSettings>(json) ?? new AiOptimizationSettings();
    }

    public static void Save(AiOptimizationSettings settings)
    {
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
