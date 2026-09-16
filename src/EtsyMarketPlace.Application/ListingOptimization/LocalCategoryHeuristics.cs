namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;

public static class LocalCategoryHeuristics
{
    private sealed record Rule(
        long TaxonomyId,
        string CategoryPath,
        string[] Keywords,
        int Priority = 1);

    private static readonly Rule[] Rules =
    [
        // Lighting - Night Lights (Specialized Sub-category - Priority 4)
        new(1042, "Home & Living > Lighting > Night Lights",
            ["night light", "gece lambasi", "nightlight", "led night light", "bedside night light", "kids night light", "bedside lamp", "ambient lamp", "astronaut lamp", "moon lamp", "nursery lamp", "sleeping light", "ambient light"],
            Priority: 4),
        new(1041, "Home & Living > Lighting > Lamps",
            ["lamp", "lamba", "desk lamp", "table lamp", "chandelier", "abajur", "lighting", "aydinlatma", "led light"],
            Priority: 2),

        // Headphone & Headset Stands (Specialized audio/desk accessory - High Priority)
        new(2079, "Electronics & Accessories > Audio > Headphone & Headset Stands",
            ["headphone stand", "kulaklik standi", "headset stand", "headphone holder", "kulaklik tutucu", "kulaklik askisi", "audio stand", "kulaklik"],
            Priority: 3),

        // Drinkware & Mugs
        new(943, "Home & Living > Kitchen & Dining > Drinkware > Mugs",
            ["mug", "kupa", "coffee mug", "kahve kupasi", "cup", "fincan", "ceramic mug", "tumbler", "coaster", "bardak"],
            Priority: 3),

        // Candle Holders & Candles
        new(1063, "Home & Living > Home Decor > Candleholders",
            ["candleholder", "candle holder", "candlestick", "tealight", "candle stand", "mumluk", "samdan", "candle", "mum"],
            Priority: 3),

        // Wall Decor & Signs
        new(1054, "Home & Living > Home Decor > Wall Decor",
            ["wall decor", "duvar dekoru", "wall hanging", "wall sign", "duvar panosu", "metal wall art", "wood wall art", "wooden wall art", "wall art"],
            Priority: 3),

        // Bags, Purses & Wallets
        new(142, "Bags & Purses > Wallets & Money Clips",
            ["wallet", "cuzdan", "card holder", "kartlik", "money clip", "bifold wallet", "leather wallet"],
            Priority: 3),

        // Cosplay & Costume Props
        new(108, "Accessories > Costume Accessories",
            ["cosplay", "helmet", "kask", "mask", "maske", "prop replica", "sword", "dagger", "armor"],
            Priority: 3),

        // 3D Printed Sculptures, Busts & Statues
        new(1239, "Art & Collectibles > Sculptures > Busts & Statues",
            ["bust", "bustu", "statue", "heykel", "figurine", "figur", "sculpture", "3d printed bust", "gollum", "smeagol", "character statue", "anime figure", "miniature"],
            Priority: 2),

        // Planters & Pots
        new(992, "Home & Living > Outdoor & Gardening > Planters & Pots",
            ["planter", "pot", "saksi", "flower pot", "succulent planter", "vase", "vazo"],
            Priority: 2),

        // Pet Supplies - Collars & Leashes
        new(982, "Pet Supplies > Pet Collars & Leashes",
            ["dog collar", "kopek tasmasi", "cat collar", "kedi tasmasi", "pet collar", "tasma", "dog leash", "leash", "pet harness"],
            Priority: 3),

        // Toys & Games - Board Games & Chess
        new(1381, "Toys & Games > Games & Puzzles > Board Games",
            ["chess set", "satranc takimi", "chess board", "satranc", "board game", "tabletop game", "dice set", "wooden chess"],
            Priority: 3),

        // Jewelry - Necklaces
        new(204, "Jewelry > Necklaces",
            ["necklace", "kolye", "pendant", "choker"],
            Priority: 2),

        // Jewelry - Rings
        new(220, "Jewelry > Rings",
            ["ring", "yuzuk", "band"],
            Priority: 2),

        // Accessories - Keychains
        new(100, "Accessories > Keychains & Lanyards",
            ["keychain", "anahtarlik", "lanyard", "bag charm"],
            Priority: 2),

        // 3D Printer Files & Craft Supplies
        new(68, "Craft Supplies & Tools > 3D Printer Files & Models",
            ["stl", "3d model", "stl file", "craft supply", "3d file", "digital file"],
            Priority: 2),

        // Digital Art & Prints
        new(1251, "Art & Collectibles > Prints > Digital Prints",
            ["digital print", "digital download", "printable", "dijital baski", "poster print", "instant download"],
            Priority: 2),

        // Desk Accessories & Organization (Medium/Generic)
        new(6701, "Home & Living > Office & School Supplies > Desk Accessories",
            ["desk organizer", "desk stand", "masa ustu", "masa duzenleyici", "pen holder", "kalemlik", "controller stand", "kol standi", "gamepad stand", "desk decor"],
            Priority: 1),

        // Home Decor & Decorative Objects (General)
        new(1053, "Home & Living > Home Decor",
            ["home decor", "ev dekoru", "decorative", "dekoratif", "shelf decor", "living room", "room decor", "table decor"],
            Priority: 1),

        // Clothing - T-Shirts & Hoodies
        new(502, "Clothing > Unisex Adult Clothing > Tops & Tees",
            ["t-shirt", "tshirt", "tisort", "shirt", "tee"],
            Priority: 2),
        new(503, "Clothing > Unisex Adult Clothing > Hoodies & Sweatshirts",
            ["hoodie", "sweatshirt", "kapusonlu"],
            Priority: 2)
    ];

    public static CategorySuggestionResult SuggestFromText(string title, string? description = null, string? tags = null)
    {
        var rawCombined = $"{title} {description} {tags}";
        var normalizedText = NormalizeForMatching(rawCombined);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return new CategorySuggestionResult(
                1239,
                "Art & Collectibles > 3D Printed / Sculptures",
                50,
                "Varsayılan kategori seçildi (Başlık boş).",
                "Heuristic-Default");
        }

        var scored = Rules
            .Select(rule =>
            {
                int matchCount = 0;
                int weight = 0;
                foreach (var kw in rule.Keywords)
                {
                    var normalizedKw = NormalizeForMatching(kw);
                    if (normalizedText.Contains(normalizedKw, StringComparison.OrdinalIgnoreCase))
                    {
                        matchCount++;
                        weight += normalizedKw.Length > 8 ? 25 : 15;
                    }
                }

                int score = Math.Min(98, weight * rule.Priority);
                return new { Rule = rule, Score = score, MatchCount = matchCount };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.MatchCount)
            .ToList();

        if (scored.Count == 0)
        {
            return new CategorySuggestionResult(
                1239,
                "Art & Collectibles > Sculptures > Busts & Statues",
                55,
                "Belirgin anahtar kelime bulunamadı; en genel 3D sanat kategorisi seçildi.",
                "Heuristic-Fallback");
        }

        var top = scored[0];
        var alternatives = scored
            .Skip(1)
            .Take(3)
            .Select(x => new TaxonomyCandidate(x.Rule.TaxonomyId, x.Rule.CategoryPath, x.Score))
            .ToList();

        return new CategorySuggestionResult(
            top.Rule.TaxonomyId,
            top.Rule.CategoryPath,
            top.Score,
            $"'{string.Join(", ", top.Rule.Keywords.Where(kw => normalizedText.Contains(NormalizeForMatching(kw), StringComparison.OrdinalIgnoreCase)).Take(2))}' ifadeleri tespit edildi.",
            "Heuristic-Rules",
            alternatives);
    }

    public static string NormalizeForMatching(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text
            .Replace('ı', 'i')
            .Replace('İ', 'i')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'g')
            .Replace('ü', 'u')
            .Replace('Ü', 'u')
            .Replace('ş', 's')
            .Replace('Ş', 's')
            .Replace('ö', 'o')
            .Replace('Ö', 'o')
            .Replace('ç', 'c')
            .Replace('Ç', 'c')
            .ToLowerInvariant();
    }
}
