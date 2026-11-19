using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EtsyMarketPlace.Application.ListingOptimization;

internal static class ProductAwareVariationHeuristic
{
    private static readonly Regex MetricSizeRegex = new(
        @"\b(\d+(?:[.,]\d+)?)\s*(?:x\s*\d+(?:[.,]\d+)?\s*)?(cm|mm)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex VolumeOzMlRegex = new(
        @"\b(\d+(?:[.,]\d+)?)\s*(oz|ml)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InchSizeRegex = new(
        @"\b(\d+(?:[.,]\d+)?(?:\s*x\s*\d+(?:[.,]\d+)?)?)\s*(?:in(?:ch|ches)?|"")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly (string Pattern, string Label)[] ColorMappings =
    [
        (@"\b(matte\s+black|mat\s+siyah)\b", "Matte Black"),
        (@"\b(black|siyah)\b", "Matte Black"),
        (@"\b(ivory\s+white|ivory|fildişi|ekru)\b", "Ivory White"),
        (@"(?<!ivory\s+)\b(white|beyaz)\b", "Warm White"),
        (@"\b(walnut|ceviz)\b", "Walnut"),
        (@"\b(oak|meşe)\b", "Natural Oak"),
        (@"\b(bronze|bronz)\b", "Metallic Bronze"),
        (@"\b(rose\s+gold)\b", "Rose Gold"),
        (@"(?<!rose\s+)\b(gold|altın)\b", "Champagne Gold"),
        (@"\b(silver|gümüş)\b", "Silver"),
        (@"\b(copper|bakır)\b", "Copper"),
        (@"\b(sage\s+green|adaçayı\s+yeşil)\b", "Sage Green"),
        (@"(?<!sage\s+)\b(emerald|zümrüt|green|yeşil)\b", "Forest Green"),
        (@"\b(navy\s+blue|navy|lacivert)\b", "Navy Blue"),
        (@"(?<!navy\s+)\b(ice\s+blue|buz\s+mavi|blue|mavi)\b", "Cobalt Blue"),
        (@"\b(terracotta|kiremit)\b", "Terracotta"),
        (@"\b(pink|pembe|blush)\b", "Blush Pink"),
        (@"\b(purple|mor|lavender)\b", "Lavender"),
        (@"\b(gray|grey|gri|slate|stone)\b", "Stone Gray"),
        (@"\b(rainbow|gökkuşa)\b", "Rainbow Silk"),
        (@"\b(clear|şeffaf|transparent|crystal)\b", "Clear Crystal")
    ];

    public static (List<SuggestedVariationGroupDto> Groups, string AnalysisSummary) Suggest(
        string? title,
        string? description,
        string? category,
        IReadOnlyList<string>? tags,
        IReadOnlyList<string>? materials,
        string? listingType)
    {
        var cleanTitle = (title ?? string.Empty).Trim();
        var cleanDesc = (description ?? string.Empty).Trim();
        var cleanCat = (category ?? string.Empty).Trim();
        var tagText = tags is { Count: > 0 } ? string.Join(" ", tags) : string.Empty;
        var matText = materials is { Count: > 0 } ? string.Join(" ", materials) : string.Empty;
        var combined = $"{cleanTitle} {cleanDesc} {cleanCat} {tagText} {matText}".ToLowerInvariant();
        var isDigital = string.Equals(listingType, "digital", StringComparison.OrdinalIgnoreCase)
            || MatchesAnyWord(combined, "digital download", "instant download", "printable", "stl file", "3d file", "svg bundle", "pdf pattern", "digital file");

        var detectedColors = ExtractColors(cleanTitle + " " + cleanDesc);
        var detectedCm = ExtractUniqueNumbers(MetricSizeRegex, cleanTitle + " " + cleanDesc, "cm");
        var detectedVolumes = ExtractVolumeLabels(cleanTitle + " " + cleanDesc);
        var detectedInches = ExtractInchLabels(cleanTitle + " " + cleanDesc);

        var groups = new List<SuggestedVariationGroupDto>();
        string archetypeNote;

        var isCandleHolder = MatchesAnyWord(combined, "candle holder", "candle holders", "candleholder", "candleholders")
            || MatchesAnyTurkishStem(combined, "mumluk");

        if (isDigital)
        {
            if (MatchesAnyWord(combined, "stl", "3d model", "3d print file", "obj", "3mf"))
            {
                groups.Add(new SuggestedVariationGroupDto
                {
                    Name = "License / File Pack",
                    Values = ["Personal STL", "Pre-Supported + STL", "Commercial License"],
                    Reason = "3D baskı dijital dosyalarında alıcıların lisans ve destekli dosya ihtiyacına göre ayrıştırıldı."
                });
                archetypeNote = "Dijital 3D model dosyası";
            }
            else
            {
                groups.Add(new SuggestedVariationGroupDto
                {
                    Name = "Print Size / Format",
                    Values = detectedInches.Count >= 2
                        ? detectedInches.Take(4).ToList()
                        : ["A4 + 8x10 in", "A3 + 12x16 in", "Full Size Bundle (5 Ratios)"],
                    Reason = "Dijital baskı ürünlerinde alıcının çerçeve ölçüsüne göre seçim yapabilmesi sağlandı."
                });
                archetypeNote = "Dijital indirilebilir baskı ürünü";
            }
        }
        else if (MatchesAnyWord(combined, "mug", "mugs", "coffee cup", "coffee cups", "tea cup", "tea cups", "tumbler", "tumblers", "drinkware")
            || MatchesAnyTurkishStem(combined, "kupa", "bardak"))
        {
            var caps = detectedVolumes.Count >= 2
                ? detectedVolumes.Take(4).ToList()
                : (detectedVolumes.Count == 1
                    ? BuildMugCapacityPair(detectedVolumes[0])
                    : ["11 oz", "15 oz"]);

            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Capacity",
                Values = caps,
                Reason = "Kupa ve içecek kaplarında Etsy alıcıları hacme göre filtreleme yapar."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Accent Color",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(4).ToList()
                    : ["Classic White", "Matte Black", "Sage Glaze"],
                Reason = "Sır/kulp rengi seçeneği kupa dönüşümünü ve hediye sepetini artırır."
            });
            archetypeNote = "Kupa / içecek kabı";
        }
        else if (MatchesAnyWord(combined, "t-shirt", "t-shirts", "tee", "tees", "hoodie", "hoodies", "sweatshirt", "sweatshirts", "dress", "dresses", "shirt", "shirts", "apparel")
            || MatchesAnyTurkishStem(combined, "tişört", "kapüşon"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Size",
                Values = ["S", "M", "L", "XL"],
                Reason = "Giyim kategorisinde temel beden seçenekleri zorunludur."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Color",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(4).ToList()
                    : ["Black", "Sand", "Forest Green", "Heather Gray"],
                Reason = "Başlık ve açıklamaya uyumlu giyim renk paleti eklendi."
            });
            archetypeNote = "Giyim ürünü";
        }
        else if (MatchesAnyWord(combined, "necklace", "necklaces", "pendant", "pendants", "bracelet", "bracelets")
            || MatchesAnyTurkishStem(combined, "kolye", "bileklik"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Chain Length",
                Values = ["16 inches", "18 inches", "20 inches"],
                Reason = "Kolye ve bilekliklerde ölçü seçeneği iade oranını düşürür."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Metal Finish",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(3).ToList()
                    : ["Sterling Silver", "18K Gold Plated", "Rose Gold"],
                Reason = "Takı kaplama seçenekleri ortalama sipariş tutarını yükseltir."
            });
            archetypeNote = "Takı (kolye/bileklik)";
        }
        else if (MatchesAnyWord(combined, "earring", "earrings")
            || MatchesAnyTurkishStem(combined, "küpe"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Metal Finish",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(3).ToList()
                    : ["Sterling Silver", "Gold Plated", "Antique Brass"],
                Reason = "Küpe alıcılarının en sık seçtiği metal bitişleri belirlendi."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Backing Style",
                Values = ["Standard Post", "Leverback", "Clip-On"],
                Reason = "Küpe klips/arka tip seçeneği müşteri kitlesini genişletir."
            });
            archetypeNote = "Küpe";
        }
        else if (MatchesAnyWord(combined, "ring", "rings")
            || MatchesAnyTurkishStem(combined, "yüzük"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Ring Size",
                Values = ["US 6", "US 7", "US 8", "US 9"],
                Reason = "Yüzük ilanlarında ABD numara seçimi satın alma kararını hızlandırır."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Metal Finish",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(3).ToList()
                    : ["Sterling Silver", "18K Gold", "Rose Gold"],
                Reason = "Kaplama tonu tercihi eklendi."
            });
            archetypeNote = "Yüzük";
        }
        else if (!isCandleHolder && (MatchesAnyWord(combined, "candle", "candles", "soy wax", "beeswax", "wax melt", "wax melts")
            || MatchesAnyTurkishStem(combined, "mum")))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Jar Size",
                Values = detectedVolumes.Count >= 2
                    ? detectedVolumes.Take(3).ToList()
                    : ["4 oz Travel", "8 oz Standard", "12 oz Double Wick"],
                Reason = "Mum ağırlığı ve yanma süresine göre fiyat kademesi oluşturuldu."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Scent",
                Values = ["Vanilla Sandalwood", "Cedar & Amber", "Unscented"],
                Reason = "Koku seçeneği hediye amaçlı alımlarda dönüşümü artırır."
            });
            archetypeNote = "Mum / kokulu ev ürünü";
        }
        else if (MatchesAnyWord(combined, "lamp", "lamps", "night light", "night lights", "led", "lantern", "lanterns", "lighting")
            || MatchesAnyTurkishStem(combined, "lamba", "abajur"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Light Color",
                Values = ["Warm White LED", "Cool Daylight", "RGB + Remote"],
                Reason = "Aydınlatma ürünlerinde ışık sıcaklığı ve RGB uzaktan kumanda yüksek fiyat katmanı sunar."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Base Finish",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(3).ToList()
                    : ["Natural Oak", "Walnut Wood", "Matte Black"],
                Reason = "Gövde/taban malzemesine uygun renk opsiyonları eklendi."
            });
            archetypeNote = "Lamba / LED aydınlatma";
        }
        else if (MatchesAnyWord(combined, "planter", "planters", "flower pot", "flower pots", "vase", "vases")
            || MatchesAnyTurkishStem(combined, "saksı", "vazo"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Pot Size",
                Values = BuildSizeTierValues(detectedCm, 10, 14, 18, "Small", "Medium", "Large"),
                Reason = "Saksı ve vazolar bitki çapına göre boyut seçenekleriyle satılır."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Color",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(4).ToList()
                    : ["Matte White", "Terracotta", "Sage Green", "Matte Black"],
                Reason = "İç mekan bitkileriyle uyumlu saksı renkleri seçildi."
            });
            archetypeNote = "Saksı / vazo";
        }
        else if (MatchesAnyWord(combined, "keychain", "keychains", "keyring", "keyrings", "coaster", "coasters", "bookmark", "bookmarks", "magnet", "magnets")
            || MatchesAnyTurkishStem(combined, "anahtarlık", "bardak altlığı"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Pack Size",
                Values = ["Single (1 pc)", "Pair (2 pcs)", "Gift Set (4 pcs)"],
                Reason = "Küçük aksesuarlarda çoklu paket seçeneği kargo maliyetini dengeleyip kârı artırır."
            });
            if (detectedColors.Count >= 2)
            {
                groups.Add(new SuggestedVariationGroupDto
                {
                    Name = "Finish",
                    Values = detectedColors.Take(4).ToList(),
                    Reason = "Başlık ve açıklamada belirtilen renk tonları korundu."
                });
            }
            archetypeNote = "Küçük aksesuar / set";
        }
        else if (MatchesAnyWord(combined, "figurine", "figurines", "sculpture", "sculptures", "statue", "statues", "bust", "busts", "alien", "aliens", "dragon", "dragons", "toy", "toys", "3d print", "3d printed", "3d printing", "pla", "resin")
            || MatchesAnyTurkishStem(combined, "heykel", "figür", "biblo"))
        {
            var isPlayfulRainbow = MatchesAnyWord(combined, "dragon", "dragons", "fidget", "flexi", "unicorn", "rainbow");
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Size",
                Values = BuildSizeTierValues(detectedCm, 12, 18, 25, "Standard", "Medium", "Collector"),
                Reason = detectedCm.Count > 0
                    ? $"Başlık/açıklamada geçen ölçü ({detectedCm[0]:0.#} cm) baz alınarak ölçek kademeleri kuruldu."
                    : "Masaüstü figür ve heykellerde raf/masa boyutuna göre ölçek seçeneği dönüşümü artırır."
            });

            var defaultFinishes = isPlayfulRainbow
                ? new List<string> { "Matte Black", "Rainbow Silk", "Metallic Bronze" }
                : new List<string> { "Matte Black", "Stone Gray", "Metallic Bronze", "Ivory White" };

            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Finish",
                Values = detectedColors.Count >= 2
                    ? detectedColors.Take(4).ToList()
                    : (detectedColors.Count == 1
                        ? MergePrimaryColor(detectedColors[0], defaultFinishes)
                        : defaultFinishes),
                Reason = detectedColors.Count > 0
                    ? $"Metinde tespit edilen '{detectedColors[0]}' tonu korunarak tamamlayıcı yüzey seçenekleri eklendi."
                    : "Ürünün stiline uygun mat ve metalik yüzey bitişleri oluşturuldu."
            });
            archetypeNote = "Masaüstü figür / dekoratif obje";
        }
        else if (MatchesAnyWord(combined, "poster", "posters", "wall art", "canvas", "painting", "paintings", "wall decor", "art print", "art prints", "wall print", "wall prints", "canvas print", "canvas prints", "fine art print", "framed print", "giclee")
            || MatchesAnyTurkishStem(combined, "tablo"))
        {
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Print Size",
                Values = detectedInches.Count >= 2
                    ? detectedInches.Take(4).ToList()
                    : ["8x10 inches", "12x16 inches", "18x24 inches"],
                Reason = "Duvar dekoru için standart uluslararası çerçeve ölçüleri seçildi."
            });
            groups.Add(new SuggestedVariationGroupDto
            {
                Name = "Frame Option",
                Values = ["Unframed Print", "Matte Black Frame", "Natural Oak Frame"],
                Reason = "Çerçeveli üst katman seçeneği kâr marjını yükseltir."
            });
            archetypeNote = "Duvar sanatı / tablo";
        }
        else
        {
            if (detectedCm.Count > 0)
            {
                groups.Add(new SuggestedVariationGroupDto
                {
                    Name = "Size",
                    Values = BuildSizeTierValues(detectedCm, 15, 22, 30, "Small", "Medium", "Large"),
                    Reason = $"Açıklama/başlıktaki ölçü ipucuna ({detectedCm[0]:0.#} cm) göre boyut kademesi oluşturuldu."
                });
            }
            else
            {
                groups.Add(new SuggestedVariationGroupDto
                {
                    Name = "Edition",
                    Values = ["Standard", "Gift Boxed", "Personalized"],
                    Reason = "El yapımı ürünlerde hediye kutusu ve kişiselleştirme seçeneği ortalama sepet tutarını artırır."
                });
            }

            if (detectedColors.Count >= 2)
            {
                groups.Add(new SuggestedVariationGroupDto
                {
                    Name = "Color",
                    Values = detectedColors.Take(4).ToList(),
                    Reason = "Başlık ve açıklamadaki renk ifadeleri doğrudan varyasyon seçeneğine dönüştürüldü."
                });
            }
            else if (groups.Count == 1 && !MatchesAnyWord(groups[0].Name, "Color", "Finish"))
            {
                groups.Add(new SuggestedVariationGroupDto
                {
                    Name = "Finish",
                    Values = detectedColors.Count == 1
                        ? MergePrimaryColor(detectedColors[0], ["Natural Finish", "Matte Black", "Warm White"])
                        : ["Natural Finish", "Matte Black", "Antique Bronze"],
                    Reason = "Alıcılara yüzey/renk tercihi sunmak için tamamlayıcı grup eklendi."
                });
            }

            archetypeNote = string.IsNullOrWhiteSpace(cleanCat) ? "Özel el yapımı ürün" : cleanCat;
        }

        var detailHints = new List<string>();
        if (detectedCm.Count > 0) detailHints.Add($"{detectedCm[0]:0.#} cm ölçü");
        if (detectedVolumes.Count > 0) detailHints.Add(string.Join("/", detectedVolumes.Take(2)));
        if (detectedColors.Count > 0) detailHints.Add(string.Join(", ", detectedColors.Take(2)));

        var hintSuffix = detailHints.Count > 0
            ? $" Metinde tespit edilen ({string.Join(" · ", detailHints)}) ipuçları korundu."
            : string.Empty;

        var summary = $"Ürün başlığı, açıklama ve kategori ({archetypeNote}) analiz edilerek {groups.Count} adet ürüne özel varyasyon grubu önerildi.{hintSuffix}";
        return (groups, summary);
    }

    public static List<SuggestedVariationOfferingDto> BuildOfferings(
        IReadOnlyList<SuggestedVariationGroupDto> groups,
        decimal basePriceUsd,
        IReadOnlyList<IReadOnlyList<decimal>>? groupMultipliers = null)
    {
        if (groups.Count == 0 || groups[0].Values.Count == 0)
            return [];

        var safeBase = basePriceUsd > 0m ? basePriceUsd : 24.90m;
        var g1 = groups[0].Values.Take(5).ToList();
        var g2 = groups.Count > 1 && groups[1].Values.Count > 0
            ? groups[1].Values.Take(5).ToList()
            : [];

        var m1 = groupMultipliers is { Count: > 0 } ? groupMultipliers[0] : null;
        var m2 = groupMultipliers is { Count: > 1 } ? groupMultipliers[1] : null;

        var offerings = new List<SuggestedVariationOfferingDto>();
        if (g2.Count == 0)
        {
            for (int i = 0; i < g1.Count; i++)
            {
                var mult = ResolveMultiplier(m1, i, g1.Count, isPrimaryDimension: true);
                offerings.Add(new SuggestedVariationOfferingDto
                {
                    Key = g1[i],
                    Price = RoundPsychologicalOrStep(safeBase * mult),
                    Quantity = 10,
                    Active = true
                });
            }
            return offerings;
        }

        for (int i = 0; i < g1.Count; i++)
        {
            var mult1 = ResolveMultiplier(m1, i, g1.Count, isPrimaryDimension: true);
            for (int j = 0; j < g2.Count; j++)
            {
                var mult2 = ResolveMultiplier(m2, j, g2.Count, isPrimaryDimension: false);
                offerings.Add(new SuggestedVariationOfferingDto
                {
                    Key = $"{g1[i]} / {g2[j]}",
                    Price = RoundPsychologicalOrStep(safeBase * mult1 * mult2),
                    Quantity = 5,
                    Active = true
                });
            }
        }

        return offerings;
    }

    private static decimal ResolveMultiplier(IReadOnlyList<decimal>? explicitList, int idx, int total, bool isPrimaryDimension)
    {
        if (explicitList is not null && idx < explicitList.Count && explicitList[idx] is >= 0.5m and <= 4.0m)
        {
            return explicitList[idx];
        }

        if (total <= 1 || !isPrimaryDimension)
            return 1.0m;

        return 1.0m + (idx * 0.15m);
    }

    private static decimal RoundPsychologicalOrStep(decimal raw)
    {
        if (raw <= 1m) return 1.00m;
        var rounded = Math.Round(raw, 2);
        var whole = Math.Floor(rounded);
        var cents = rounded - whole;
        if (cents < 0.20m) return Math.Max(1.90m, whole - 0.10m);
        if (cents <= 0.65m) return whole + 0.50m;
        return whole + 0.90m;
    }

    private static bool MatchesAnyWord(string haystack, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(haystack) || terms.Length == 0)
            return false;

        var escaped = string.Join("|", terms.Select(Regex.Escape));
        return Regex.IsMatch(haystack, $@"\b(?:{escaped})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool MatchesAnyTurkishStem(string haystack, params string[] stems)
    {
        if (string.IsNullOrWhiteSpace(haystack) || stems.Length == 0)
            return false;

        var escaped = string.Join("|", stems.Select(Regex.Escape));
        return Regex.IsMatch(haystack, $@"(?<![\p{{L}}\p{{N}}])(?:{escaped})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static List<string> ExtractColors(string text)
    {
        var normalized = (text ?? string.Empty).Replace('İ', 'i');
        var found = new List<string>();
        foreach (var (pattern, label) in ColorMappings)
        {
            if (Regex.IsMatch(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                !found.Contains(label, StringComparer.OrdinalIgnoreCase))
            {
                found.Add(label);
            }
        }
        return found;
    }

    private static List<double> ExtractUniqueNumbers(Regex regex, string text, string targetUnit)
    {
        var list = new List<double>();
        foreach (Match m in regex.Matches(text))
        {
            if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) || v <= 0)
                continue;

            var unit = m.Groups[2].Value.ToLowerInvariant();
            if (targetUnit == "cm" && unit == "mm") v /= 10.0;
            if (v is >= 1 and <= 200 && !list.Any(x => Math.Abs(x - v) < 0.2))
            {
                list.Add(Math.Round(v, 1));
            }
        }
        return list.OrderBy(x => x).ToList();
    }

    private static List<string> ExtractVolumeLabels(string text)
    {
        var list = new List<string>();
        foreach (Match m in VolumeOzMlRegex.Matches(text))
        {
            var label = $"{m.Groups[1].Value.Replace(',', '.')} {m.Groups[2].Value.ToLowerInvariant()}";
            if (!list.Contains(label, StringComparer.OrdinalIgnoreCase))
                list.Add(label);
        }
        return list;
    }

    private static List<string> ExtractInchLabels(string text)
    {
        var list = new List<string>();
        foreach (Match m in InchSizeRegex.Matches(text))
        {
            var clean = Regex.Replace(m.Groups[1].Value, @"\s*x\s*", "x");
            var label = $"{clean} in";
            if (!list.Contains(label, StringComparer.OrdinalIgnoreCase))
                list.Add(label);
        }
        return list;
    }

    private static List<string> BuildMugCapacityPair(string detected)
    {
        if (detected.StartsWith("11", StringComparison.OrdinalIgnoreCase)) return ["11 oz", "15 oz"];
        if (detected.StartsWith("15", StringComparison.OrdinalIgnoreCase)) return ["11 oz", "15 oz"];
        return [detected, "Gift Set (2 pcs)"];
    }

    private static List<string> BuildSizeTierValues(
        List<double> detectedCm,
        double def1,
        double def2,
        double def3,
        string label1,
        string label2,
        string label3)
    {
        if (detectedCm.Count >= 2)
        {
            return detectedCm.Take(4).Select(c => $"{c:0.#} cm").ToList();
        }

        if (detectedCm.Count == 1)
        {
            var baseCm = detectedCm[0];
            var medium = Math.Round(baseCm * 1.35, 0);
            var large = Math.Round(baseCm * 1.75, 0);
            if (medium <= baseCm) medium = baseCm + 4;
            if (large <= medium) large = medium + 5;
            return [$"{label1} ({baseCm:0.#} cm)", $"{label2} ({medium:0.#} cm)", $"{label3} ({large:0.#} cm)"];
        }

        return [$"{label1} ({def1:0.#} cm)", $"{label2} ({def2:0.#} cm)", $"{label3} ({def3:0.#} cm)"];
    }

    private static List<string> MergePrimaryColor(string primary, List<string> defaults)
    {
        var merged = new List<string> { primary };
        foreach (var d in defaults)
        {
            if (!merged.Contains(d, StringComparer.OrdinalIgnoreCase))
                merged.Add(d);
            if (merged.Count >= 4) break;
        }
        return merged;
    }
}
