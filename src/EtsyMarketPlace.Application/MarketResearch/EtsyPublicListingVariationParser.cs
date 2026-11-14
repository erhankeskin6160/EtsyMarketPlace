namespace EtsyMarketPlace.Application.MarketResearch;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

/// <summary>
/// Etsy public listing HTML sayfasından varyasyon gruplarını, seçeneklerini ve fiyatlarını
/// (örn. &lt;select id="variation-selector-0"&gt; ve gömülü fiyat etiketleri) çıkaran saf ayrıştırıcı.
/// </summary>
public static class EtsyPublicListingVariationParser
{
    private sealed record ParsedOption(string Value, decimal? PriceUsd, decimal? DeltaUsd, bool IsAvailable);

    private sealed record ParsedGroup(string Name, List<ParsedOption> Options);

    private static readonly Regex SelectRegex = new(
        @"<select\b[^>]*\bid=[""']variation-selector-(\d+)[""'][^>]*>(.*?)</select>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OptionRegex = new(
        @"<option\b[^>]*\bvalue=[""']([^""']*)[""'][^>]*>(.*?)</option>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex PriceParenRegex = new(
        @"\(\s*(\+\s*)?(?:US\s*\$|\$|USD|EUR|€|GBP|£|TRY|₺)?\s*(\d+(?:[.,]\d{1,2})?)\s*(?:USD|EUR|GBP|TRY)?\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SoldOutRegex = new(
        @"\(\s*(?:sold\s*out|out\s*of\s*stock|tükendi|stokta\s*yok)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LdJsonPriceRegex = new(
        @"""(?:lowPrice|price)""\s*:\s*""?(\d+(?:[.,]\d{1,2})?)""?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> PlaceholderTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "select an option",
        "choose an option",
        "select a color",
        "select a size",
        "select a style",
        "bir seçenek seçin",
        "seçiniz",
        "bir renk seçin",
        "bir boyut seçin"
    };

    public static CompetitorListingInventoryDto Parse(long listingId, string html)
    {
        var result = new CompetitorListingInventoryDto
        {
            ListingId = listingId,
            Source = "etsy_public_page"
        };

        if (string.IsNullOrWhiteSpace(html))
        {
            result.Source = "none";
            return result;
        }

        decimal fallbackBasePrice = ExtractFallbackBasePrice(html);

        var groups = new List<(int Index, ParsedGroup Group)>();
        foreach (Match selectMatch in SelectRegex.Matches(html))
        {
            if (!int.TryParse(selectMatch.Groups[1].Value, out int selectorIndex))
            {
                selectorIndex = groups.Count;
            }

            string selectBody = selectMatch.Groups[2].Value;
            string groupName = ExtractLabelForSelector(html, selectorIndex, selectMatch.Index)
                ?? $"Variation {selectorIndex + 1}";

            var options = new List<ParsedOption>();
            var seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match optMatch in OptionRegex.Matches(selectBody))
            {
                string optValAttr = optMatch.Groups[1].Value.Trim();
                string rawText = WebUtility.HtmlDecode(HtmlTagRegex.Replace(optMatch.Groups[2].Value, " ")).Trim();
                rawText = Regex.Replace(rawText, @"\s+", " ").Trim();

                if (string.IsNullOrWhiteSpace(optValAttr) && (string.IsNullOrWhiteSpace(rawText) || PlaceholderTexts.Contains(rawText)))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawText) || PlaceholderTexts.Contains(rawText))
                {
                    continue;
                }

                bool isAvailable = true;
                if (SoldOutRegex.IsMatch(rawText))
                {
                    isAvailable = false;
                    rawText = SoldOutRegex.Replace(rawText, "").Trim();
                }

                decimal? exactPrice = null;
                decimal? deltaPrice = null;

                var priceMatch = PriceParenRegex.Match(rawText);
                if (priceMatch.Success)
                {
                    string numStr = priceMatch.Groups[2].Value.Replace(',', '.');
                    if (decimal.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedAmt))
                    {
                        if (!string.IsNullOrWhiteSpace(priceMatch.Groups[1].Value))
                        {
                            deltaPrice = parsedAmt;
                        }
                        else
                        {
                            exactPrice = parsedAmt;
                        }
                    }

                    rawText = rawText[..priceMatch.Index].Trim();
                }

                if (string.IsNullOrWhiteSpace(rawText) || !seenValues.Add(rawText))
                {
                    continue;
                }

                options.Add(new ParsedOption(rawText, exactPrice, deltaPrice, isAvailable));
            }

            if (options.Count > 0)
            {
                groups.Add((selectorIndex, new ParsedGroup(groupName, options)));
            }
        }

        if (groups.Count == 0)
        {
            result.Source = "none";
            return result;
        }

        var orderedGroups = groups.OrderBy(g => g.Index).Select(g => g.Group).ToList();

        result.HasVariations = true;
        result.Groups = orderedGroups.Select(g => new CompetitorVariationGroupDto
        {
            Name = g.Name,
            Values = g.Options.Select(o => o.Value).ToList()
        }).ToList();

        result.VarType1 = orderedGroups[0].Name;
        result.VarValues1 = string.Join(", ", orderedGroups[0].Options.Select(o => o.Value));

        if (orderedGroups.Count > 1)
        {
            result.EnableVar2 = true;
            result.VarType2 = orderedGroups[1].Name;
            result.VarValues2 = string.Join(", ", orderedGroups[1].Options.Select(o => o.Value));
        }

        result.Offerings = BuildOfferings(orderedGroups, fallbackBasePrice);
        return result;
    }

    private static List<CompetitorVariationOfferingDto> BuildOfferings(
        List<ParsedGroup> groups,
        decimal fallbackBasePrice)
    {
        var offerings = new List<CompetitorVariationOfferingDto>();

        if (groups.Count == 1)
        {
            foreach (var opt in groups[0].Options)
            {
                decimal price = opt.PriceUsd
                    ?? (opt.DeltaUsd.HasValue ? fallbackBasePrice + opt.DeltaUsd.Value : fallbackBasePrice);

                offerings.Add(new CompetitorVariationOfferingDto
                {
                    Key = opt.Value,
                    Price = Math.Round(price, 2),
                    Quantity = opt.IsAvailable ? 10 : 0,
                    Active = opt.IsAvailable
                });
            }

            return offerings;
        }

        foreach (var opt1 in groups[0].Options)
        {
            foreach (var opt2 in groups[1].Options)
            {
                decimal price = ResolveCombinedPrice(opt1, opt2, fallbackBasePrice);
                bool active = opt1.IsAvailable && opt2.IsAvailable;

                offerings.Add(new CompetitorVariationOfferingDto
                {
                    Key = $"{opt1.Value} / {opt2.Value}",
                    Price = Math.Round(price, 2),
                    Quantity = active ? 10 : 0,
                    Active = active
                });
            }
        }

        return offerings;
    }

    private static decimal ResolveCombinedPrice(ParsedOption opt1, ParsedOption opt2, decimal fallbackBasePrice)
    {
        if (opt2.PriceUsd.HasValue && !opt1.PriceUsd.HasValue)
        {
            return opt2.PriceUsd.Value + (opt1.DeltaUsd ?? 0m);
        }

        if (opt1.PriceUsd.HasValue && !opt2.PriceUsd.HasValue)
        {
            return opt1.PriceUsd.Value + (opt2.DeltaUsd ?? 0m);
        }

        if (opt1.PriceUsd.HasValue && opt2.PriceUsd.HasValue)
        {
            return Math.Max(opt1.PriceUsd.Value, opt2.PriceUsd.Value);
        }

        return fallbackBasePrice + (opt1.DeltaUsd ?? 0m) + (opt2.DeltaUsd ?? 0m);
    }

    private static string? ExtractLabelForSelector(string html, int selectorIndex, int selectPos)
    {
        var labelPattern = $@"<label\b[^>]*\bfor=[""']variation-selector-{selectorIndex}[""'][^>]*>(.*?)</label>";
        var labelMatch = Regex.Match(html, labelPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (labelMatch.Success)
        {
            string clean = CleanGroupLabel(WebUtility.HtmlDecode(HtmlTagRegex.Replace(labelMatch.Groups[1].Value, " ")));
            if (!string.IsNullOrWhiteSpace(clean))
            {
                return clean;
            }
        }

        int lookbackStart = Math.Max(0, selectPos - 500);
        string snippet = html.Substring(lookbackStart, selectPos - lookbackStart);
        var nearbyLabels = Regex.Matches(snippet, @"<label\b[^>]*>(.*?)</label>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (nearbyLabels.Count > 0)
        {
            string lastLabel = nearbyLabels[^1].Groups[1].Value;
            string clean = CleanGroupLabel(WebUtility.HtmlDecode(HtmlTagRegex.Replace(lastLabel, " ")));
            if (!string.IsNullOrWhiteSpace(clean))
            {
                return clean;
            }
        }

        return null;
    }

    private static string CleanGroupLabel(string raw)
    {
        string text = Regex.Replace(raw, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"^(?:Choose\s+your\s+|Select\s+(?:a|your)\s+|Renginizi\s+|Tarzınızı\s+)", "", RegexOptions.IgnoreCase).Trim();
        if (text.EndsWith("seçin", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^5].Trim();
        }
        return text.TrimEnd(':', '*', ' ');
    }

    private static decimal ExtractFallbackBasePrice(string html)
    {
        var match = LdJsonPriceRegex.Match(html);
        if (match.Success &&
            decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
        {
            return price;
        }

        return 0m;
    }
}
