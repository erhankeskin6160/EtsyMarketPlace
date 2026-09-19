namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.IO;
using System.Text.Json;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// ShipEntegra ayarlarını ve oturum tokenini yerel diskte saklayan depo.
/// </summary>
public static class ShipEntegraSettingsStore
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
            return Path.Combine(folder, "shipentegra-settings.json");
        }
    }

    public static ShipEntegraSettings Load(string? customPath = null)
    {
        var path = customPath ?? SettingsPath;
        if (!File.Exists(path))
        {
            return new ShipEntegraSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ShipEntegraSettings>(json, JsonOptions) ?? new ShipEntegraSettings();
        }
        catch
        {
            return new ShipEntegraSettings();
        }
    }

    public static void Save(ShipEntegraSettings settings, string? customPath = null)
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
