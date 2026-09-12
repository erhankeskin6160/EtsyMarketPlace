namespace EtsyMarketPlace.Application.Viral3DModels.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public sealed class AiModelSearchExpander : IModelSearchExpander
{
    private static readonly Dictionary<string, (string Primary, string[] Keywords, string[] Tags, string Category)> Taxonomy =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ejderha"] = ("articulated dragon", ["flexi dragon", "print in place dragon", "crystal dragon", "baby dragon", "wyvern"], ["print-in-place", "articulated", "flexi", "silk-pla"], "Toys & Figures"),
            ["dragon"] = ("articulated dragon", ["flexi dragon", "print in place dragon", "crystal dragon", "baby dragon", "flying dragon"], ["print-in-place", "articulated", "flexi", "silk-pla"], "Toys & Figures"),
            ["oyuncak"] = ("articulated toy", ["dummy 13", "fidget toy", "flexi animal", "print in place toy", "desk pet"], ["toy", "fidget", "print-in-place"], "Toys & Figures"),
            ["toy"] = ("articulated toy", ["dummy 13", "action figure", "flexi animal", "desk pet"], ["toy", "fidget", "print-in-place"], "Toys & Figures"),
            ["figür"] = ("action figure", ["dummy 13", "lucky 13", "poseable figure", "bionic robot", "anime figure"], ["action-figure", "poseable", "jointed"], "Toys & Figures"),
            ["figure"] = ("action figure", ["dummy 13", "lucky 13", "poseable figure", "bionic robot"], ["action-figure", "poseable", "jointed"], "Toys & Figures"),
            ["robot"] = ("bionic robot figure", ["dummy 13", "articulated robot", "mech warrior", "snap fit robot"], ["robot", "jointed", "poseable"], "Toys & Figures"),
            ["fidget"] = ("fidget toy", ["infinity cube", "fidget cone", "haptic slider", "gyro sphere", "impossible pyramid"], ["fidget", "stress-relief", "edc"], "Toys & Figures"),
            ["stres"] = ("stress relief fidget", ["infinity cube", "fidget slider", "fidget cone", "gyro sphere"], ["stress-relief", "fidget"], "Toys & Figures"),
            ["ahtapot"] = ("articulated octopus", ["flexi octopus", "cute octopus", "tentacle fidget"], ["octopus", "articulated", "print-in-place"], "Toys & Figures"),
            ["octopus"] = ("articulated octopus", ["flexi octopus", "cute octopus", "tentacle fidget"], ["octopus", "articulated", "print-in-place"], "Toys & Figures"),
            ["dinozor"] = ("flexi dinosaur", ["t-rex skeleton", "articulated rex", "velociraptor", "dino fossil"], ["dinosaur", "t-rex", "flexi"], "Toys & Figures"),
            ["dinosaur"] = ("flexi dinosaur", ["t-rex skeleton", "articulated rex", "velociraptor"], ["dinosaur", "t-rex", "flexi"], "Toys & Figures"),
            ["zar"] = ("dice tower", ["castle dice tower", "spiral dice tower", "dice jail", "cthulhu dice box", "dnd dice tray"], ["dnd", "rpg", "dice-tower", "tabletop"], "Tabletop & RPG"),
            ["dice"] = ("dice tower", ["castle dice tower", "spiral dice tower", "dice jail", "cthulhu dice box"], ["dnd", "rpg", "dice-tower", "tabletop"], "Tabletop & RPG"),
            ["dnd"] = ("d&d tabletop accessories", ["dice tower", "combat tracker", "dungeon terrain", "miniature figure", "dm screen"], ["dnd-5e", "rpg", "tabletop"], "Tabletop & RPG"),
            ["kutu oyunu"] = ("tabletop board game accessories", ["dice tower", "card holder", "token tray", "deck box", "resource tracker"], ["boardgame", "tabletop", "organizer"], "Tabletop & RPG"),
            ["kale"] = ("castle dice tower", ["fortress miniature", "medieval castle", "fantasy terrain"], ["castle", "medieval", "terrain"], "Tabletop & RPG"),
            ["saksı"] = ("spiral planter", ["vortex planter", "self watering planter", "succulent pot", "hexagonal planter", "twisted vase"], ["planter", "succulent", "vase"], "Home & Garden"),
            ["planter"] = ("spiral planter", ["vortex planter", "self watering planter", "succulent pot", "modern planter"], ["planter", "succulent", "vase"], "Home & Garden"),
            ["vazo"] = ("modern spiral vase", ["vase mode", "fluted vase", "geometric vase", "minimalist vase"], ["vase-mode", "spiral", "decor"], "Home & Garden"),
            ["vase"] = ("modern spiral vase", ["vase mode", "fluted vase", "geometric vase", "minimalist vase"], ["vase-mode", "spiral", "decor"], "Home & Garden"),
            ["lamba"] = ("led lamp", ["lightbox", "lithophane frame", "moon lamp", "crystal cave lamp", "ambient night light"], ["led", "lightbox", "lamp", "night-light"], "LED Lighting & Art"),
            ["lamp"] = ("led lamp", ["lightbox", "lithophane frame", "moon lamp", "crystal cave lamp"], ["led", "lightbox", "lamp", "night-light"], "LED Lighting & Art"),
            ["lightbox"] = ("led lightbox", ["anime lightbox", "custom name signboard", "multi-plate sign", "illuminated logo"], ["lightbox", "ams", "multicolor", "led"], "LED Lighting & Art"),
            ["tabela"] = ("led signboard", ["lightbox", "neon sign", "desk sign", "light plate"], ["signboard", "lightbox", "led"], "LED Lighting & Art"),
            ["düzenleyici"] = ("modular desk organizer", ["honeycomb storage wall", "wavegrid drawer", "skadis pegboard", "cable management", "hex shelf"], ["organizer", "modular", "storage", "hsw"], "Workshop & Organization"),
            ["organizer"] = ("modular desk organizer", ["honeycomb storage wall", "wavegrid drawer", "skadis pegboard", "cable clip"], ["organizer", "modular", "storage", "hsw"], "Workshop & Organization"),
            ["çekmece"] = ("modular slide-out drawers", ["wavegrid drawers", "stackable storage box", "gridfinity bins"], ["drawers", "gridfinity", "storage"], "Workshop & Organization"),
            ["drawer"] = ("modular slide-out drawers", ["wavegrid drawers", "stackable storage box", "gridfinity bins"], ["drawers", "gridfinity", "storage"], "Workshop & Organization"),
            ["hsw"] = ("honeycomb storage wall", ["hsw hook", "hsw tool holder", "hsw shelf", "modular pegboard"], ["hsw", "honeycomb", "workshop"], "Workshop & Organization"),
            ["skadis"] = ("ikea skadis accessories", ["skadis hook", "skadis tray", "skadis tool holder", "skadis pegboard"], ["skadis", "ikea", "pegboard"], "Workshop & Organization"),
            ["kablo"] = ("cable management clip", ["magnetic cable holder", "under desk cable tray", "cord organizer"], ["cable-management", "desk-setup"], "Workshop & Organization"),
            ["kulaklık"] = ("headphone desk stand", ["under-desk headphone hook", "controller stand", "headset mount"], ["headphone-stand", "desk-setup"], "Workshop & Organization"),
            ["benchy"] = ("#3dbenchy torture test", ["3d printer test", "calibration cube", "overhang test", "speed benchy"], ["benchy", "calibration", "benchmark"], "Workshop & Organization")
        };

    public Task<ModelSearchQueryExpansion> ExpandQueryAsync(
        string userQuery,
        ShopNicheProfile? shopProfile = null,
        CancellationToken ct = default)
    {
        string raw = (userQuery ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (shopProfile != null && shopProfile.AffinityKeywords.Count > 0)
            {
                string firstKw = shopProfile.AffinityKeywords.FirstOrDefault() ?? "3d print";
                return ExpandQueryAsync(firstKw, shopProfile, ct);
            }
            return Task.FromResult(ModelSearchQueryExpansion.CreateDefault("3d print"));
        }

        string lower = raw.ToLowerInvariant();
        var matchedTerms = new List<string>();

        // Match against taxonomy
        foreach (var kvp in Taxonomy)
        {
            if (lower.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                matchedTerms.Add(kvp.Key);
            }
        }

        if (matchedTerms.Count > 0)
        {
            string bestKey = matchedTerms.OrderByDescending(k => k.Length).First();
            var tax = Taxonomy[bestKey];

            var kwList = new List<string> { tax.Primary };
            kwList.AddRange(tax.Keywords);
            if (!kwList.Contains(raw, StringComparer.OrdinalIgnoreCase))
            {
                kwList.Add(raw);
            }

            return Task.FromResult(new ModelSearchQueryExpansion
            {
                OriginalQuery = raw,
                PrimaryEnglishTerm = tax.Primary,
                SearchKeywords = kwList.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Technical3DTags = tax.Tags,
                TargetCategory = tax.Category,
                CommercialIntentOnly = true,
                AiExplanation = $"AI Anlamsal Çıkarım: '{raw}' sorgusu '{tax.Category}' kategorisinde '{tax.Primary}' ve {tax.Keywords.Length} alternatif 3D anahtar kelimeye genişletildi."
            });
        }

        // Default heuristic expansion for unknown terms
        var defaultKeywords = new List<string>
        {
            raw,
            $"{raw} 3d print",
            $"{raw} articulated",
            $"{raw} model"
        };

        return Task.FromResult(new ModelSearchQueryExpansion
        {
            OriginalQuery = raw,
            PrimaryEnglishTerm = raw,
            SearchKeywords = defaultKeywords,
            Technical3DTags = ["3d print", "maker", "stl"],
            TargetCategory = "General",
            CommercialIntentOnly = true,
            AiExplanation = $"AI Doğrudan Tarama: '{raw}' için 3D baskı etiketleri oluşturuldu."
        });
    }
}
