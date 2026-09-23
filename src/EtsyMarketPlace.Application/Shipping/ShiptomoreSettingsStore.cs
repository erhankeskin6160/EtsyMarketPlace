namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.IO;
using System.Text.Json;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Shiptomore ayarlarını, oturum bilgilerini ve çerezlerini yerel diskte saklayan depo.
/// </summary>
public static class ShiptomoreSettingsStore
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
            return Path.Combine(folder, "shiptomore-settings.json");
        }
    }

    public static ShiptomoreSettings Load(string? customPath = null)
    {
        var path = customPath ?? SettingsPath;
        if (!File.Exists(path))
        {
            return new ShiptomoreSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ShiptomoreSettings>(json, JsonOptions) ?? new ShiptomoreSettings();
        }
        catch
        {
            return new ShiptomoreSettings();
        }
    }

    public static void Save(ShiptomoreSettings settings, string? customPath = null)
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
