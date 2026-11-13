namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Normalizes AI-generated Etsy tags to strictly comply with Etsy's 20-character / 13-tag limits
/// without sacrificing SEO quality or requiring extra LLM round-trips.
/// </summary>
public static class EtsyTagNormalizer
{
    public const int MaxTagLength = 20;
    public const int TargetTagCount = 13;

    private static readonly Regex InvalidTagCharsRegex = new(@"[^a-z0-9\s\-]", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly HashSet<string> FillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "for", "with", "and", "the", "in", "of", "to", "a", "an", "by", "on", "from"
    };

    /// <summary>
    /// Selects up to 13 unique tags <= 20 chars:
    /// 1) Preserves already-valid AI tags untouched (including backup candidates 14-16).
    /// 2) Word-boundary compacts over-20-char AI tags without mid-word truncation.
    /// 3) Fills any remaining slots up to 13 from fallback candidates if provided.
    /// </summary>
    public static List<string> NormalizeTo13Tags(
        IEnumerable<string>? aiCandidates,
        IEnumerable<string>? fallbackCandidates = null)
    {
        var result = new List<string>(TargetTagCount);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var oversized = new List<string>();

        if (aiCandidates != null)
        {
            foreach (var raw in aiCandidates)
            {
                var cleaned = CleanTag(raw);
                if (string.IsNullOrWhiteSpace(cleaned))
                    continue;

                if (cleaned.Length <= MaxTagLength)
                {
                    if (seen.Add(cleaned))
                    {
                        result.Add(cleaned);
                        if (result.Count == TargetTagCount)
                            return result;
                    }
                }
                else
                {
                    oversized.Add(cleaned);
                }
            }
        }

        // Compact oversized AI tags at word boundaries if backup valid tags were not enough
        foreach (var longTag in oversized)
        {
            if (result.Count >= TargetTagCount)
                break;

            var compacted = CompactAtWordBoundary(longTag);
            if (!string.IsNullOrWhiteSpace(compacted) && compacted.Length <= MaxTagLength && seen.Add(compacted))
            {
                result.Add(compacted);
            }
        }

        // Fill remaining slots from deterministic rule-based fallback tags if needed
        if (result.Count < TargetTagCount && fallbackCandidates != null)
        {
            foreach (var raw in fallbackCandidates)
            {
                if (result.Count >= TargetTagCount)
                    break;

                var cleaned = CleanTag(raw);
                if (string.IsNullOrWhiteSpace(cleaned))
                    continue;

                var candidate = cleaned.Length <= MaxTagLength ? cleaned : CompactAtWordBoundary(cleaned);
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= MaxTagLength && seen.Add(candidate))
                {
                    result.Add(candidate);
                }
            }
        }

        return result;
    }

    public static string? CompactAtWordBoundary(string cleanedTag)
    {
        if (string.IsNullOrWhiteSpace(cleanedTag))
            return null;

        if (cleanedTag.Length <= MaxTagLength)
            return cleanedTag;

        var words = cleanedTag.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count == 0)
            return null;

        // Step 1: Drop filler/stop words first (e.g. "gift for anime fans" -> "gift anime fans")
        var withoutFillers = words.Where(w => !FillerWords.Contains(w)).ToList();
        if (withoutFillers.Count >= 2)
        {
            var joined = string.Join(' ', withoutFillers);
            if (joined.Length <= MaxTagLength)
                return joined;
            words = withoutFillers;
        }

        // Step 2: Trim trailing words while keeping multi-word phrase intact
        var trailingTrimmed = new List<string>(words);
        while (trailingTrimmed.Count > 1)
        {
            trailingTrimmed.RemoveAt(trailingTrimmed.Count - 1);
            var candidate = string.Join(' ', trailingTrimmed);
            if (candidate.Length <= MaxTagLength)
                return candidate;
        }

        // Step 3: Try dropping leading modifier word (e.g. "personalized cosplay prop" -> "cosplay prop")
        var leadingTrimmed = new List<string>(words);
        while (leadingTrimmed.Count > 1)
        {
            leadingTrimmed.RemoveAt(0);
            var candidate = string.Join(' ', leadingTrimmed);
            if (candidate.Length <= MaxTagLength)
                return candidate;
        }

        // Step 4: Single word <= 20 chars fallback
        var shortestValidWord = words.FirstOrDefault(w => w.Length >= 2 && w.Length <= MaxTagLength);
        return shortestValidWord;
    }

    private static string CleanTag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var lower = raw.Trim().ToLowerInvariant();
        var stripped = InvalidTagCharsRegex.Replace(lower, " ");
        return MultiSpaceRegex.Replace(stripped, " ").Trim();
    }
}

