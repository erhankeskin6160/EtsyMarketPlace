namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.IO;
using System.Text.Json;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras Global ayarlarını ve oturum tokenini yerel diskte saklayan depo.
/// </summary>
public static class ArasGlobalSettingsStore
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
            return Path.Combine(folder, "aras-global-settings.json");
        }
    }

    public static ArasGlobalSettings Load(string? customPath = null)
    {
        var path = customPath ?? SettingsPath;
        if (!File.Exists(path))
        {
            return new ArasGlobalSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ArasGlobalSettings>(json, JsonOptions) ?? new ArasGlobalSettings();
        }
        catch
        {
            return new ArasGlobalSettings();
        }
    }

    public static void Save(ArasGlobalSettings settings, string? customPath = null)
    {
        try
        {
            var path = customPath ?? SettingsPath;
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch { }
    }
}
