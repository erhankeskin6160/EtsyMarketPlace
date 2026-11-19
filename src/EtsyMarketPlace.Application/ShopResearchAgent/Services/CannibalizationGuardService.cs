namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;

/// <summary>
/// V2 Hybrid Cannibalization Guard combining:
/// 0.45 * Character Trigram Cosine (catches morphology/compound variations) +
/// 0.35 * Token Jaccard (exact keyword overlap) +
/// 0.20 * Price Band Overlap (same buyer price segment).
/// </summary>
public sealed class CannibalizationGuardService
{
    public CannibalizationCheckResult Check(
        string candidateTitle,
        IReadOnlyList<string> candidateTags,
        IReadOnlyList<ShopListingItemDto> ownListings,
        decimal? candidatePriceUsd = null)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle) || ownListings == null || ownListings.Count == 0)
        {
            return new CannibalizationCheckResult(0, false, null, null);
        }

        var candTokens = Tokenize(candidateTitle, candidateTags);
        var candTrigrams = BuildTrigrams($"{candidateTitle} {string.Join(" ", candidateTags ?? Array.Empty<string>())}");
        if (candTokens.Count == 0)
        {
            return new CannibalizationCheckResult(0, false, null, null);
        }

        double maxHybrid = 0;
        string? matchedTitle = null;

        foreach (var listing in ownListings)
        {
            if (string.IsNullOrWhiteSpace(listing.Title)) continue;

            var ownTokens = Tokenize(listing.Title, listing.Tags);
            if (ownTokens.Count == 0) continue;

            var jaccard = ComputeJaccard(candTokens, ownTokens);
            var ownTrigrams = BuildTrigrams($"{listing.Title} {string.Join(" ", listing.Tags ?? Array.Empty<string>())}");
            var trigramCosine = ComputeCosine(candTrigrams, ownTrigrams);

            double priceOverlap = 0.0;
            var ownPrice = listing.Price > 0 ? listing.Price : listing.PriceAmount;
            if (candidatePriceUsd.HasValue && candidatePriceUsd.Value > 0 && ownPrice > 0)
            {
                var ratioDiff = Math.Abs((double)(candidatePriceUsd.Value - ownPrice)) / (double)Math.Max(candidatePriceUsd.Value, ownPrice);
                priceOverlap = ratioDiff <= 0.20 ? 1.0 : ratioDiff <= 0.40 ? 0.5 : 0.0;
            }

            // If no price is provided, re-normalize weights across trigram + jaccard
            var hybrid = candidatePriceUsd.HasValue && ownPrice > 0
                ? (0.45 * trigramCosine) + (0.35 * jaccard) + (0.20 * priceOverlap)
                : (0.55 * trigramCosine) + (0.45 * jaccard);

            if (hybrid > maxHybrid)
            {
                maxHybrid = hybrid;
                matchedTitle = listing.Title;
            }
        }

        var score = Math.Clamp((int)Math.Round(maxHybrid * 100), 0, 100);
        if (score >= 70)
        {
            return new CannibalizationCheckResult(
                score,
                true,
                matchedTitle,
                $"Yüksek yamyamlaşma riski (%{score} hibrit benzerlik): '{matchedTitle}' ilanınızla aynı alıcı segmentinde çakışıyor.");
        }

        if (score >= 48)
        {
            return new CannibalizationCheckResult(
                score,
                false,
                matchedTitle,
                $"Yakın ikame uyarısı (%{score} hibrit benzerlik): '{matchedTitle}' ile farklılaştırarak listeleyin.");
        }

        return new CannibalizationCheckResult(score, false, matchedTitle, null);
    }

    public double ComputeTitleSimilarity(string titleA, string titleB)
    {
        if (string.IsNullOrWhiteSpace(titleA) || string.IsNullOrWhiteSpace(titleB)) return 0d;
        var tA = Tokenize(titleA, null);
        var tB = Tokenize(titleB, null);
        var gA = BuildTrigrams(titleA);
        var gB = BuildTrigrams(titleB);
        return (0.55 * ComputeCosine(gA, gB)) + (0.45 * ComputeJaccard(tA, tB));
    }

    private static double ComputeJaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0d;
        var intersection = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        var union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0d : (double)intersection / union;
    }

    private static Dictionary<string, int> BuildTrigrams(string input)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(input)) return dict;
        var norm = new string(input.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();
        if (norm.Length < 3) return dict;

        for (var i = 0; i <= norm.Length - 3; i++)
        {
            var tri = norm.Substring(i, 3);
            dict[tri] = dict.TryGetValue(tri, out var count) ? count + 1 : 1;
        }
        return dict;
    }

    private static double ComputeCosine(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0d;
        double dot = 0, magA = 0, magB = 0;
        foreach (var kv in a)
        {
            magA += kv.Value * kv.Value;
            if (b.TryGetValue(kv.Key, out var bv))
            {
                dot += kv.Value * bv;
            }
        }
        foreach (var kv in b)
        {
            magB += kv.Value * kv.Value;
        }
        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom == 0 ? 0d : dot / denom;
    }

    private static HashSet<string> Tokenize(string title, IReadOnlyList<string>? tags)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddWords(set, title);
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                AddWords(set, tag);
            }
        }
        return set;
    }

    private static void AddWords(HashSet<string> target, string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;
        foreach (var part in input.Split([' ', ',', '-', '|', '/', '(', ')', ':'], StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = part.Trim().ToLowerInvariant();
            if (clean.Length >= 4 && clean.EndsWith('s') && !clean.EndsWith("ss", StringComparison.Ordinal))
            {
                clean = clean[..^1];
            }
            if (clean.Length >= 3)
            {
                target.Add(clean);
            }
        }
    }
}
