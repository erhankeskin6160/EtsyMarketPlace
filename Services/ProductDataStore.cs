namespace SimilarProductsWinForms.Services;

using System.Text.Json;

internal static class ProductDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DataPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "product-ideas.json");
        }
    }

    public static List<ProductCandidate> Load()
    {
        if (!File.Exists(DataPath))
        {
            return ProductCandidate.Seed();
        }

        try
        {
            var json = File.ReadAllText(DataPath);
            var products = JsonSerializer.Deserialize<List<ProductCandidate>>(json, JsonOptions);
            return products is { Count: > 0 } ? products : ProductCandidate.Seed();
        }
        catch
        {
            return ProductCandidate.Seed();
        }
    }

    public static void Save(IEnumerable<ProductCandidate> products)
    {
        var json = JsonSerializer.Serialize(products, JsonOptions);
        File.WriteAllText(DataPath, json);
    }
}
