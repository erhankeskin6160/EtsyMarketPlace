namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Domain.ShopResearchAgent;

/// <summary>
/// Extracts and updates a structured ShopDnaProfile from the user's own active Etsy listings,
/// preserving user-defined constraints such as forbidden keywords and schedule cadence.
/// </summary>
public sealed class ShopDnaBuilderService
{
    private static readonly string[] ThreeDPrintSignals =
    [
        "3d print", "3d printed", "pla", "stl", "resin", "articulated", "fidget",
        "figurine", "miniature", "statue", "bust", "planter", "keycap"
    ];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "gift", "custom", "handmade", "etsy", "home", "decor",
        "size", "inch", "set", "pack", "new", "best", "sale", "free", "shipping"
    };

    public ShopDnaProfile BuildFromListings(
        string ownerUserId,
        string shopId,
        IReadOnlyList<ShopListingItemDto> listings,
        ShopDnaProfile? existingProfile = null)
    {
        var active = listings?.Where(l => !string.IsNullOrWhiteSpace(l.Title)).ToList() ?? [];

        var (priceMin, priceMax) = ComputePriceBand(active, existingProfile);
        var topTags = ExtractTopTags(active);
        var categoryKeywords = ExtractCategoryKeywords(active, topTags);
        var materials = ExtractMaterials(active, existingProfile);
        var supports3D = Detect3DPrintSupport(active, existingProfile);
        var primaryNiche = InferPrimaryNiche(active, topTags, supports3D, existingProfile);

        return new ShopDnaProfile
        {
            OwnerUserId = ownerUserId,
            ShopId = shopId,
            DnaVersion = (existingProfile?.DnaVersion ?? 0) + 1,
            PrimaryNiche = primaryNiche,
            TargetAudience = existingProfile?.TargetAudience ?? (supports3D ? "Koleksiyoncular, Masaüstü & Hediye Alıcıları" : "Özgün Tasarım & Hediye Alıcıları"),
            TargetPriceMinUsd = priceMin,
            TargetPriceMaxUsd = priceMax,
            MinAcceptableNetMarginPct = existingProfile?.MinAcceptableNetMarginPct ?? 25m,
            EstimatedCogsRatio = existingProfile?.EstimatedCogsRatio ?? 0.35m,
            Supports3DPrint = supports3D,
            PreferredMaterials = materials,
            TopPerformingTags = topTags,
            CategoryKeywords = categoryKeywords,
            ForbiddenKeywords = existingProfile?.ForbiddenKeywords ?? Array.Empty<string>(),
            ScheduleCadence = existingProfile?.ScheduleCadence ?? ResearchScheduleCadence.Off,
            LastScheduledRunAtUtc = existingProfile?.LastScheduledRunAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            DefaultShippingCostUsd = existingProfile?.DefaultShippingCostUsd ?? 0m,
            DefaultPackagingCostUsd = existingProfile?.DefaultPackagingCostUsd ?? 1.00m,
            ReturnReserveRatePct = existingProfile?.ReturnReserveRatePct ?? 4.0m
        };
    }

    private static (decimal Min, decimal Max) ComputePriceBand(
        IReadOnlyList<ShopListingItemDto> active,
        ShopDnaProfile? existing)
    {
        var prices = active
            .Select(l => l.Price > 0 ? l.Price : l.PriceAmount)
            .Where(p => p > 0m)
            .OrderBy(p => p)
            .ToList();

        if (prices.Count == 0)
        {
            return (existing?.TargetPriceMinUsd ?? 15m, existing?.TargetPriceMaxUsd ?? 75m);
        }

        var p20 = prices[(int)Math.Floor((prices.Count - 1) * 0.20)];
        var p85 = prices[(int)Math.Floor((prices.Count - 1) * 0.85)];
        var min = Math.Max(5m, Math.Round(p20 * 0.85m, 2));
        var max = Math.Max(min + 10m, Math.Round(p85 * 1.25m, 2));
        return (min, max);
    }

    private static IReadOnlyList<string> ExtractTopTags(IReadOnlyList<ShopListingItemDto> active)
    {
        return active
            .OrderByDescending(l => l.Favorites + l.NumFavorers + (l.Views / 10))
            .SelectMany(l => l.Tags ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(15)
            .Select(g => g.Key)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractCategoryKeywords(
        IReadOnlyList<ShopListingItemDto> active,
        IReadOnlyList<string> topTags)
    {
        var titleTokens = active
            .SelectMany(l => (l.Title ?? string.Empty).Split([' ', ',', '-', '|', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length >= 3 && !StopWords.Contains(w) && !int.TryParse(w, out _))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(12)
            .Select(g => g.Key);

        return topTags.Concat(titleTokens).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
    }

    private static IReadOnlyList<string> ExtractMaterials(
        IReadOnlyList<ShopListingItemDto> active,
        ShopDnaProfile? existing)
    {
        var found = active
            .SelectMany(l => l.Materials ?? Array.Empty<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .GroupBy(m => m, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(8)
            .Select(g => g.Key)
            .ToList();

        return found.Count > 0 ? found : (existing?.PreferredMaterials ?? Array.Empty<string>());
    }

    private static bool Detect3DPrintSupport(
        IReadOnlyList<ShopListingItemDto> active,
        ShopDnaProfile? existing)
    {
        if (active.Count == 0 && existing != null)
            return existing.Supports3DPrint;

        var corpus = string.Join(" ", active.Select(l =>
            $"{l.Title} {string.Join(" ", l.Tags ?? Array.Empty<string>())} {string.Join(" ", l.Materials ?? Array.Empty<string>())}"));

        var hits = ThreeDPrintSignals.Count(sig => corpus.Contains(sig, StringComparison.OrdinalIgnoreCase));
        return hits >= 2 || (existing?.Supports3DPrint ?? false);
    }

    private static string InferPrimaryNiche(
        IReadOnlyList<ShopListingItemDto> active,
        IReadOnlyList<string> topTags,
        bool supports3D,
        ShopDnaProfile? existing)
    {
        if (active.Count == 0)
            return existing?.PrimaryNiche ?? "Genel Etsy Mağazası";

        var topCluster = string.Join(" / ", topTags.Take(3));
        if (string.IsNullOrWhiteSpace(topCluster))
            return supports3D ? "3D Baskı & Özel Tasarım Ürünler" : "Özel Tasarım & El Yapımı Ürünler";

        return supports3D
            ? $"3D Baskı & Özel Tasarım ({topCluster})"
            : $"Etsy Özel Niş ({topCluster})";
    }
}
