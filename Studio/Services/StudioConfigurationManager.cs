namespace SimilarProductsWinForms.Studio.Services;

using System;
using System.IO;
using System.Text.Json;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

public sealed class StudioConfiguration
{
    public string PhotoRoomApiKey { get; set; } = string.Empty;
    public string GoogleGeminiApiKey { get; set; } = string.Empty;
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string BflApiKey { get; set; } = string.Empty;
    public string IdeogramApiKey { get; set; } = string.Empty;

    public string DefaultEngineId { get; set; } = "photoroom";
    public string DefaultGeminiModel { get; set; } = "gemini-3.1-flash-image";
    public string DefaultOpenAiModel { get; set; } = "gpt-image-2";
    public string DefaultFluxModel { get; set; } = "flux-pro-1.1";

    public bool AddSoftShadow { get; set; } = true;
    public double DefaultPadding { get; set; } = 0.10;
}

public static class StudioConfigurationManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static StudioConfiguration? _cachedConfig;

    public static string ConfigPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "ai-studio-config.json");
        }
    }

    public static StudioConfiguration Current => _cachedConfig ??= Load();

    public static StudioConfiguration Load()
    {
        try
        {
            StudioConfiguration config;
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                config = JsonSerializer.Deserialize<StudioConfiguration>(json) ?? new StudioConfiguration();
            }
            else
            {
                config = new StudioConfiguration();
            }

            // Fallback & Seamless Auto-Import from legacy stores if fields are blank
            bool updated = false;

            if (string.IsNullOrWhiteSpace(config.PhotoRoomApiKey))
            {
                var prLegacy = PhotoRoomSettingsStore.Load();
                if (!string.IsNullOrWhiteSpace(prLegacy.ApiKey))
                {
                    config.PhotoRoomApiKey = prLegacy.ApiKey.Trim();
                    updated = true;
                }
            }

            var aiLegacy = AiOptimizationSettingsStore.Load();
            if (string.IsNullOrWhiteSpace(config.GoogleGeminiApiKey) && !string.IsNullOrWhiteSpace(aiLegacy.GeminiApiKey))
            {
                config.GoogleGeminiApiKey = aiLegacy.GeminiApiKey.Trim();
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(config.OpenAiApiKey) && !string.IsNullOrWhiteSpace(aiLegacy.OpenAiApiKey))
            {
                config.OpenAiApiKey = aiLegacy.OpenAiApiKey.Trim();
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(config.BflApiKey) && !string.IsNullOrWhiteSpace(aiLegacy.BflApiKey))
            {
                config.BflApiKey = aiLegacy.BflApiKey.Trim();
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(config.IdeogramApiKey) && !string.IsNullOrWhiteSpace(aiLegacy.IdeogramApiKey))
            {
                config.IdeogramApiKey = aiLegacy.IdeogramApiKey.Trim();
                updated = true;
            }

            if (string.IsNullOrWhiteSpace(config.PhotoRoomApiKey) && !string.IsNullOrWhiteSpace(aiLegacy.PhotoRoomApiKey))
            {
                config.PhotoRoomApiKey = aiLegacy.PhotoRoomApiKey.Trim();
                updated = true;
            }

            _cachedConfig = config;
            if (updated)
            {
                Save(config);
            }

            return config;
        }
        catch
        {
            _cachedConfig = new StudioConfiguration();
            return _cachedConfig;
        }
    }

    public static void Save(StudioConfiguration config)
    {
        try
        {
            _cachedConfig = config;
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);

            // Synchronize back to legacy stores so older parts of application remain 100% functional
            PhotoRoomSettingsStore.Save(new PhotoRoomSettings
            {
                ApiKey = config.PhotoRoomApiKey,
                AddShadow = config.AddSoftShadow,
                Padding = config.DefaultPadding
            });

            var legacyAi = AiOptimizationSettingsStore.Load();
            legacyAi.PhotoRoomApiKey = config.PhotoRoomApiKey;
            legacyAi.GeminiApiKey = config.GoogleGeminiApiKey;
            legacyAi.OpenAiApiKey = config.OpenAiApiKey;
            legacyAi.BflApiKey = config.BflApiKey;
            legacyAi.IdeogramApiKey = config.IdeogramApiKey;
            AiOptimizationSettingsStore.Save(legacyAi);
        }
        catch { }
    }
}
