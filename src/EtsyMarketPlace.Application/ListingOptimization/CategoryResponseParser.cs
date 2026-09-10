namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Text.Json;

public static class CategoryResponseParser
{
    public static CategorySuggestionResult? Parse(string rawText, string provider)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;

        string cleanJson = StripJsonFences(rawText);
        try
        {
            using var doc = JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;

            long taxonomyId = 0;
            if (root.TryGetProperty("taxonomy_id", out var idProp))
            {
                if (idProp.ValueKind == JsonValueKind.Number) taxonomyId = idProp.GetInt64();
                else if (idProp.ValueKind == JsonValueKind.String && long.TryParse(idProp.GetString(), out var parsedId)) taxonomyId = parsedId;
            }

            string categoryPath = root.TryGetProperty("category_path", out var pathProp) ? pathProp.GetString() ?? "" : "";
            int confidence = root.TryGetProperty("confidence_score", out var confProp) && confProp.ValueKind == JsonValueKind.Number ? confProp.GetInt32() : 90;
            string reasoning = root.TryGetProperty("reasoning", out var rProp) ? rProp.GetString() ?? "" : "";

            var alternatives = new List<TaxonomyCandidate>();
            if (root.TryGetProperty("alternatives", out var altArr) && altArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in altArr.EnumerateArray())
                {
                    long altId = 0;
                    if (item.TryGetProperty("taxonomy_id", out var aId))
                    {
                        if (aId.ValueKind == JsonValueKind.Number) altId = aId.GetInt64();
                        else if (aId.ValueKind == JsonValueKind.String && long.TryParse(aId.GetString(), out var parsedAlt)) altId = parsedAlt;
                    }

                    string altPath = item.TryGetProperty("category_path", out var aPath) ? aPath.GetString() ?? "" : "";
                    int altConf = item.TryGetProperty("confidence_score", out var aConf) && aConf.ValueKind == JsonValueKind.Number ? aConf.GetInt32() : 75;

                    if (altId > 0 && !string.IsNullOrWhiteSpace(altPath))
                    {
                        alternatives.Add(new TaxonomyCandidate(altId, altPath, altConf));
                    }
                }
            }

            if (taxonomyId > 0 && !string.IsNullOrWhiteSpace(categoryPath))
            {
                return new CategorySuggestionResult(taxonomyId, categoryPath, confidence, reasoning, provider, alternatives);
            }
        }
        catch
        {
            // JSON parse error
        }

        return null;
    }

    public static string StripJsonFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(7);
        }
        else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(3);
        }

        if (trimmed.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }

        return trimmed.Trim();
    }
}
