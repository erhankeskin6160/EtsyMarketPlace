namespace EtsyMarketPlace.Infrastructure.Viral3DModels.Services;

using System;
using System.IO;

public static class Viral3DModelAssetManager
{
    private static readonly string[] AssetSearchDirectories =
    [
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "3DModels"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "3DModels"),
        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "3DModels")
    ];

    public static string? ResolveLocalAssetPath(string imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName)) return null;

        string cleanName = imageName.Replace("asset://", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (!cleanName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !cleanName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            cleanName += ".jpg";
        }

        foreach (var dir in AssetSearchDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    string candidate = Path.Combine(dir, cleanName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // Ignore IO errors when probing paths
            }
        }

        return null;
    }

    public static string GetAssetForModel(string title, string? fallbackUrl = null)
    {
        string lower = (title ?? string.Empty).ToLowerInvariant();

        if (lower.Contains("benchy")) return "asset://benchy.jpg";
        if (lower.Contains("dummy") || lower.Contains("action figure") || lower.Contains("jointed action")) return "asset://dummy13.jpg";
        if (lower.Contains("dragon") || lower.Contains("ejderha")) return "asset://dragon.jpg";
        if (lower.Contains("octopus") || lower.Contains("ahtapot")) return "asset://octopus.jpg";
        if (lower.Contains("lightbox") || lower.Contains("signboard") || lower.Contains("led sign")) return "asset://lightbox.jpg";
        if (lower.Contains("wavegrid") || lower.Contains("drawer") || lower.Contains("organizer")) return "asset://wavegrid.jpg";
        if (lower.Contains("planter") || lower.Contains("saksı") || lower.Contains("spiral")) return "asset://planter.jpg";
        if (lower.Contains("dice tower") || lower.Contains("castle") || lower.Contains("zar")) return "asset://dicetower.jpg";
        if (lower.Contains("lamp") || lower.Contains("crystal") || lower.Contains("lamba")) return "asset://crystal_lamp.jpg";
        if (lower.Contains("hsw") || lower.Contains("honeycomb") || lower.Contains("shelf") || lower.Contains("pegboard")) return "asset://hsw_shelf.jpg";

        // If fallbackUrl is valid and not picsum, return it; otherwise return dummy13 or benchy
        if (!string.IsNullOrWhiteSpace(fallbackUrl) &&
            !fallbackUrl.Contains("picsum", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackUrl;
        }

        return "asset://dummy13.jpg";
    }
}
