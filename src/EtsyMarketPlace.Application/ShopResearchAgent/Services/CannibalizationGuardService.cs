namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;

/// <summary>
/// Prevents product cannibalization by comparing candidate products against the store's own active listings.
/// </summary>
public sealed class CannibalizationGuardService
{
    public CannibalizationCheckResult Check(
        string candidateTitle,
        IReadOnlyList<string> candidateTags,
        IReadOnlyList<ShopListingItemDto> ownListings)
    {
        if (string.IsNullOrWhiteSpace(candidateTitle) || ownListings == null || ownListings.Count == 0)
        {
            return new CannibalizationCheckResult(0, false, null, null);
        }

        var candTokens = Tokenize(candidateTitle, candidateTags);
        if (candTokens.Count == 0)
        {
            return new CannibalizationCheckResult(0, false, null, null);
        }

        double maxSimilarity = 0;
        string? matchedTitle = null;

        foreach (var listing in ownListings)
        {
            if (string.IsNullOrWhiteSpace(listing.Title)) continue;

            var ownTokens = Tokenize(listing.Title, listing.Tags);
            if (ownTokens.Count == 0) continue;

            var intersection = candTokens.Intersect(ownTokens, StringComparer.OrdinalIgnoreCase).Count();
            var union = candTokens.Union(ownTokens, StringComparer.OrdinalIgnoreCase).Count();
            var jaccard = union == 0 ? 0d : (double)intersection / union;

            if (jaccard > maxSimilarity)
            {
                maxSimilarity = jaccard;
                matchedTitle = listing.Title;
            }
        }

        var score = (int)Math.Round(maxSimilarity * 100);
        if (score >= 70)
        {
            return new CannibalizationCheckResult(
                score,
                true,
                matchedTitle,
                $"Yüksek yamyamlaşma riski (%{score} benzerlik): '{matchedTitle}' ilanınızla neredeyse aynı.");
        }

        if (score >= 45)
        {
            return new CannibalizationCheckResult(
                score,
                false,
                matchedTitle,
                $"Yakın ikame uyarısı (%{score} benzerlik): '{matchedTitle}' ile farklılaştırarak listeleyin.");
        }

        return new CannibalizationCheckResult(score, false, matchedTitle, null);
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
            if (clean.Length >= 3)
            {
                target.Add(clean);
            }
        }
    }
}
