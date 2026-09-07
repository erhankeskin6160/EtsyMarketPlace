namespace SimilarProductsWinForms.Services;

using System.IO;
using System.Text.Json;

public static class NotificationSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EtsyMarketPlace",
        "notification-settings.json");

    public static NotificationSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new NotificationSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<NotificationSettings>(json) ?? new NotificationSettings();
        }
        catch
        {
            return new NotificationSettings();
        }
    }

    public static void Save(NotificationSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}
