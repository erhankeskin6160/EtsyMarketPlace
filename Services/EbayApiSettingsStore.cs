namespace SimilarProductsWinForms.Services;

using System.Text.Json;
using EtsyMarketPlace.Infrastructure.ExternalMarketplaces;

internal static class EbayApiSettingsStore
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
            return Path.Combine(folder, "ebay-api-settings.json");
        }
    }

    public static EbayApiSettings Load()
    {
        var settings = File.Exists(SettingsPath)
            ? JsonSerializer.Deserialize<EbayApiSettings>(File.ReadAllText(SettingsPath)) ?? new EbayApiSettings()
            : new EbayApiSettings();

        settings.ClientId = FirstNonEmpty(settings.ClientId, Environment.GetEnvironmentVariable("EBAY_CLIENT_ID"));
        settings.ClientSecret = FirstNonEmpty(settings.ClientSecret, Environment.GetEnvironmentVariable("EBAY_CLIENT_SECRET"));
        settings.MarketplaceId = FirstNonEmpty(settings.MarketplaceId, Environment.GetEnvironmentVariable("EBAY_MARKETPLACE_ID"), "EBAY_US");
        settings.Limit = settings.Limit <= 0 ? 20 : settings.Limit;
        return settings;
    }

    public static void Save(EbayApiSettings settings)
    {
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
