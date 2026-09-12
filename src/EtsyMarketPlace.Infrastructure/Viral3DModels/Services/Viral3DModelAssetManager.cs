namespace EtsyMarketPlace.Infrastructure.Viral3DModels.Services;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

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

        string cleanName = NormalizeImageName(imageName);

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

    /// <summary>
    /// Opens a readable Stream for the requested 3D model asset.
    /// Checks local disk first; if not found (e.g. running as single-file standalone on VDS),
    /// loads directly from embedded resources inside the compiled assembly.
    /// </summary>
    public static Stream? OpenAssetStream(string imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName)) return null;

        string cleanName = NormalizeImageName(imageName);

        // 1. Try local disk path
        string? localPath = ResolveLocalAssetPath(cleanName);
        if (localPath != null && File.Exists(localPath))
        {
            try
            {
                return File.OpenRead(localPath);
            }
            catch
            {
                // Fallback to embedded stream
            }
        }

        // 2. Try Embedded Resources across executing/loaded assemblies (Single-file VDS support)
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                if (asm.IsDynamic) continue;

                string[] resourceNames;
                try
                {
                    resourceNames = asm.GetManifestResourceNames();
                }
                catch
                {
                    continue;
                }

                // Match resource ending in cleanName or "Assets._3DModels.{cleanName}"
                string? targetRes = resourceNames.FirstOrDefault(r =>
                    r.EndsWith(cleanName, StringComparison.OrdinalIgnoreCase) ||
                    r.EndsWith($".{cleanName}", StringComparison.OrdinalIgnoreCase));

                if (targetRes != null)
                {
                    var resStream = asm.GetManifestResourceStream(targetRes);
                    if (resStream != null)
                    {
                        return resStream;
                    }
                }
            }
        }
        catch
        {
            // Ignore assembly inspection exceptions
        }

        return null;
    }

    /// <summary>
    /// Reads asset bytes directly into memory.
    /// </summary>
    public static byte[]? GetAssetBytes(string imageName)
    {
        using var stream = OpenAssetStream(imageName);
        if (stream == null) return null;

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string NormalizeImageName(string imageName)
    {
        string clean = imageName.Replace("asset://", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (!clean.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !clean.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            clean += ".jpg";
        }
        return clean;
    }

    /// <summary>
    /// Intelligently matches a 3D model title/category/tags to the highest quality visual asset.
    /// </summary>
    public static string GetAssetForModel(string title, string? fallbackUrl = null)
    {
        string lower = (title ?? string.Empty).ToLowerInvariant();

        // 1. Benchy / Marine / Boat
        if (lower.Contains("benchy") || lower.Contains("boat") || lower.Contains("gemi") || lower.Contains("torture test"))
            return "asset://benchy.jpg";

        // 2. Articulated Action Figure / DUMMY 13 / Robots
        if (lower.Contains("dummy") || lower.Contains("action figure") || lower.Contains("jointed action") ||
            lower.Contains("robot") || lower.Contains("bionic") || lower.Contains("warrior") || lower.Contains("figurine"))
            return "asset://dummy13.jpg";

        // 3. Dragons / Flexi Animals / Reptiles
        if (lower.Contains("dragon") || lower.Contains("ejderha") || lower.Contains("axolotl") ||
            lower.Contains("dinosaur") || lower.Contains("t-rex") || lower.Contains("snake") ||
            lower.Contains("lizard") || lower.Contains("gecko") || lower.Contains("chameleon") ||
            lower.Contains("croc") || lower.Contains("alligator") || lower.Contains("flexi pet"))
            return "asset://dragon.jpg";

        // 4. Octopus / Marine / Fidget Animals
        if (lower.Contains("octopus") || lower.Contains("ahtapot") || lower.Contains("squid") ||
            lower.Contains("fidget toy") || lower.Contains("fidget cube") || lower.Contains("fidget cone") ||
            lower.Contains("gyro") || lower.Contains("clicker") || lower.Contains("pop-it"))
            return "asset://octopus.jpg";

        // 5. Lightbox / Signs / Illuminated Art
        if (lower.Contains("lightbox") || lower.Contains("signboard") || lower.Contains("led sign") ||
            lower.Contains("neon") || lower.Contains("tabela") || lower.Contains("anime light") ||
            lower.Contains("illuminated") || lower.Contains("shadow box"))
            return "asset://lightbox.jpg";

        // 6. WaveGrid / Modular Drawers / Organization
        if (lower.Contains("wavegrid") || lower.Contains("drawer") || lower.Contains("organizer") ||
            lower.Contains("çekmece") || lower.Contains("düzenleyici") || lower.Contains("storage box") ||
            lower.Contains("gridfinity") || lower.Contains("box") || lower.Contains("kutusu") ||
            lower.Contains("cable clip") || lower.Contains("kablo"))
            return "asset://wavegrid.jpg";

        // 7. Planters / Vases / Botanical Decor
        if (lower.Contains("planter") || lower.Contains("saksı") || lower.Contains("spiral") ||
            lower.Contains("vazo") || lower.Contains("vase") || lower.Contains("flower") ||
            lower.Contains("botanic") || lower.Contains("propagation") || lower.Contains("succulent") ||
            lower.Contains("kaktüs"))
            return "asset://planter.jpg";

        // 8. Dice Towers / RPG / Castles / Tabletop
        if (lower.Contains("dice tower") || lower.Contains("castle") || lower.Contains("zar") ||
            lower.Contains("dungeon") || lower.Contains("d&d") || lower.Contains("rpg") ||
            lower.Contains("dice jail") || lower.Contains("kale") || lower.Contains("miniature terrain") ||
            lower.Contains("tabletop"))
            return "asset://dicetower.jpg";

        // 9. Lamps / Crystals / Ambient Lighting
        if (lower.Contains("lamp") || lower.Contains("crystal") || lower.Contains("lamba") ||
            lower.Contains("cave") || lower.Contains("lithophane") || lower.Contains("moon lamp") ||
            lower.Contains("gece lambası") || lower.Contains("abajur") || lower.Contains("chandelier"))
            return "asset://crystal_lamp.jpg";

        // 10. Honeycomb Storage Wall (HSW) / Pegboard / Shelves
        if (lower.Contains("hsw") || lower.Contains("honeycomb") || lower.Contains("shelf") ||
            lower.Contains("pegboard") || lower.Contains("skadis") || lower.Contains("atölye") ||
            lower.Contains("wall mount") || lower.Contains("askı") || lower.Contains("raf"))
            return "asset://hsw_shelf.jpg";

        // If fallbackUrl is valid and not picsum, return it; otherwise return dummy13
        if (!string.IsNullOrWhiteSpace(fallbackUrl) &&
            !fallbackUrl.Contains("picsum", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackUrl;
        }

        return "asset://dummy13.jpg";
    }
}
