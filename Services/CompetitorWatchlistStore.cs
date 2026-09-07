namespace SimilarProductsWinForms.Services;

using System.Text.Json;

internal static class CompetitorWatchlistStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EtsyMarketPlace",
        "competitor_watchlist.json");

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return ["3DArtDesignsStore"];
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list is { Count: > 0 } ? list : ["3DArtDesignsStore"];
        }
        catch
        {
            return ["3DArtDesignsStore"];
        }
    }

    public static void Add(string shopName)
    {
        if (string.IsNullOrWhiteSpace(shopName)) return;
        var clean = shopName.Trim();
        var list = Load();
        if (!list.Contains(clean, StringComparer.OrdinalIgnoreCase))
        {
            list.Insert(0, clean);
            Save(list);
        }
    }

    public static void Remove(string shopName)
    {
        var list = Load();
        list.RemoveAll(s => s.Equals(shopName.Trim(), StringComparison.OrdinalIgnoreCase));
        Save(list);
    }

    private static void Save(List<string> list)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}
