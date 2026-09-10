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

            // Studio Configuration'dan otomatik anahtar tamamlama
            string studioCfgPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms",
                "ai-studio-config.json");

            if (File.Exists(studioCfgPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(studioCfgPath));
                    var root = doc.RootElement;
                    if (string.IsNullOrWhiteSpace(settings.OpenAiApiKey) && root.TryGetProperty("OpenAiApiKey", out var oKey) && !string.IsNullOrWhiteSpace(oKey.GetString()))
                    {
                        settings.OpenAiApiKey = oKey.GetString()!.Trim();
                        needsSave = true;
                    }
                    if (string.IsNullOrWhiteSpace(settings.GeminiApiKey) && root.TryGetProperty("GoogleGeminiApiKey", out var gKey) && !string.IsNullOrWhiteSpace(gKey.GetString()))
                    {
                        settings.GeminiApiKey = gKey.GetString()!.Trim();
                        needsSave = true;
                    }
                    if (string.IsNullOrWhiteSpace(settings.PhotoRoomApiKey) && root.TryGetProperty("PhotoRoomApiKey", out var pKey) && !string.IsNullOrWhiteSpace(pKey.GetString()))
                    {
                        settings.PhotoRoomApiKey = pKey.GetString()!.Trim();
                        needsSave = true;
                    }
                }
                catch { }
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

        // StudioConfigurationManager ile de senkronize et
        try
        {
            string studioCfgPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms",
                "ai-studio-config.json");

            var studioDict = new Dictionary<string, object>();
            if (File.Exists(studioCfgPath))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(studioCfgPath));
                    if (existing != null) studioDict = existing;
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(settings.OpenAiApiKey)) studioDict["OpenAiApiKey"] = settings.OpenAiApiKey.Trim();
            if (!string.IsNullOrWhiteSpace(settings.GeminiApiKey)) studioDict["GoogleGeminiApiKey"] = settings.GeminiApiKey.Trim();
            if (!string.IsNullOrWhiteSpace(settings.PhotoRoomApiKey)) studioDict["PhotoRoomApiKey"] = settings.PhotoRoomApiKey.Trim();

            File.WriteAllText(studioCfgPath, JsonSerializer.Serialize(studioDict, JsonOptions));
        }
        catch { }
    }
}
