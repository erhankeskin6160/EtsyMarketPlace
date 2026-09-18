namespace SimilarProductsWinForms.Services;

using System;
using System.IO;
using System.Text.Json;
using SimilarProductsWinForms.Models;

/// <summary>
/// Hızlı Ürün Ekle taslağını yerel diske JSON olarak güvenle kaydeden ve geri yükleyen servis.
/// </summary>
internal static class FastListingDraftStore
{
    private static readonly string DraftFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EtsyMarketPlace",
        "fast_listing_draft.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void SaveDraft(FastListingDraftModel draft)
    {
        try
        {
            if (draft == null || draft.IsEmpty)
            {
                ClearDraft();
                return;
            }

            var dir = Path.GetDirectoryName(DraftFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            draft.LastSavedAt = DateTimeOffset.UtcNow;
            string json = JsonSerializer.Serialize(draft, JsonOpts);
            File.WriteAllText(DraftFilePath, json, System.Text.Encoding.UTF8);
        }
        catch { /* ignore persistence errors */ }
    }

    public static FastListingDraftModel? LoadDraft()
    {
        try
        {
            if (!File.Exists(DraftFilePath))
            {
                return null;
            }

            string json = File.ReadAllText(DraftFilePath, System.Text.Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var draft = JsonSerializer.Deserialize<FastListingDraftModel>(json, JsonOpts);
            return draft;
        }
        catch
        {
            return null;
        }
    }

    public static void ClearDraft()
    {
        try
        {
            if (File.Exists(DraftFilePath))
            {
                File.Delete(DraftFilePath);
            }
        }
        catch { }
    }
}
