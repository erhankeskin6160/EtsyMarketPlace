namespace SimilarProductsWinForms.Services;

using System.Text.Json;
using SimilarProductsWinForms.Models;

internal static class EtsyApiSettingsStore
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
            return Path.Combine(folder, "etsy-api-settings.json");
        }
    }

    public static EtsyApiSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new EtsyApiSettings();
        }

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<EtsyApiSettings>(json) ?? new EtsyApiSettings();
    }

    public static void Save(EtsyApiSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
