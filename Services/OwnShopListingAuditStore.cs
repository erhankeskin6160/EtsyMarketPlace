namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SimilarProductsWinForms.Models;

/// <summary>
/// Mağaza ürünleri için yapay zeka denetim sonuçlarını (SEO puanı, AI hedef puanı, eksikler ve öneriler)
/// diske kalıcı olarak kaydeden ve geri yükleyen thread-safe JSON depolama servisi.
/// </summary>
internal static class OwnShopListingAuditStore
{
    private static readonly object FileLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string FilePath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "own-shop-listing-audits.json");
        }
    }

    /// <summary>
    /// Diskte kayıtlı tüm AI listing denetimlerini ListingId anahtarıyla yükler.
    /// </summary>
    public static Dictionary<long, SavedListingAuditData> LoadAll()
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new Dictionary<long, SavedListingAuditData>();
                }

                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new Dictionary<long, SavedListingAuditData>();
                }

                var list = JsonSerializer.Deserialize<List<SavedListingAuditData>>(json, JsonOptions);
                var dict = new Dictionary<long, SavedListingAuditData>();
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        dict[item.ListingId] = item;
                    }
                }
                return dict;
            }
            catch
            {
                return new Dictionary<long, SavedListingAuditData>();
            }
        }
    }

    /// <summary>
    /// Tek bir listing için yapay zeka denetim verisini kaydeder veya günceller.
    /// </summary>
    public static void Save(SavedListingAuditData data)
    {
        if (data == null) return;

        lock (FileLock)
        {
            try
            {
                var dict = LoadAll();
                dict[data.ListingId] = data;

                var list = new List<SavedListingAuditData>(dict.Values);
                var json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // Sessizce yut, ana akışı bozma
            }
        }
    }

    /// <summary>
    /// Toplu denetim sonuçlarını tek seferde diske yazar.
    /// </summary>
    public static void SaveBatch(IEnumerable<SavedListingAuditData> items)
    {
        if (items == null) return;

        lock (FileLock)
        {
            try
            {
                var dict = LoadAll();
                foreach (var item in items)
                {
                    dict[item.ListingId] = item;
                }

                var list = new List<SavedListingAuditData>(dict.Values);
                var json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // Sessizce yut
            }
        }
    }

    /// <summary>
    /// Belirtilen ListingId için kayıtlı AI denetimi varsa döner.
    /// </summary>
    public static bool TryGet(long listingId, out SavedListingAuditData? data)
    {
        var dict = LoadAll();
        return dict.TryGetValue(listingId, out data);
    }
}
