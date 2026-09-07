namespace SimilarProductsWinForms.Services;

using System.IO;
using System.Text.Json;

public sealed class PhotoRoomSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public bool AddShadow { get; set; } = true;
    public double Padding { get; set; } = 0.1; // 10% padding
}

public static class PhotoRoomSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EtsyMarketPlace",
        "photoroom-settings.json");

    public static PhotoRoomSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new PhotoRoomSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<PhotoRoomSettings>(json) ?? new PhotoRoomSettings();
        }
        catch
        {
            return new PhotoRoomSettings();
        }
    }

    public static void Save(PhotoRoomSettings settings)
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
