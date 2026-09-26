namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>Bir gövde karşılaştırma farkının türü.</summary>
public enum PayloadDiffKind
{
    /// <summary>Alan bizim gövdemizde var, karşı tarafta yok.</summary>
    MissingInTheirs,

    /// <summary>Alan karşı tarafta var, bizim gövdelerinde yok.</summary>
    MissingInOurs,

    /// <summary>Alan iki tarafta da var ama değeri farklı.</summary>
    ValueDiffers
}

/// <summary>Tek bir alan farkı.</summary>
public sealed class PayloadDiff
{
    public PayloadDiffKind Kind { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Ours { get; init; } = string.Empty;
    public string Theirs { get; init; } = string.Empty;

    public override string ToString() => Kind switch
    {
        PayloadDiffKind.MissingInTheirs => $"[YALNIZ BIZDE]     {Path} = {Ours}",
        PayloadDiffKind.MissingInOurs => $"[YALNIZ ONLARDA]   {Path} = {Theirs}",
        _ => $"[DEGER FARKLI]     {Path}: bizde '{Ours}' / onlarda '{Theirs}'"
    };
}

/// <summary>
/// İki JSON gövdesini düzleştirip alan alan karşılaştırır.
/// Amaç: Aras panelinin gönderdiği gövde ile bizim gönderdiğimizi yan yana koyup
/// "hangi alan eksik / hangi değer farklı" sorusunu tahmine bırakmadan yanıtlamak.
/// </summary>
public static class ArasPayloadComparer
{
    public static IReadOnlyList<PayloadDiff> Compare(string? oursJson, string? theirsJson)
        => Compare(oursJson, theirsJson, Array.Empty<string>());

    /// <param name="ignorePathFragments">
    /// İçinde bu parçalardan biri geçen yollar karşılaştırma dışı bırakılır
    /// (ör. değişken kimlikler: "shipmentId", "referenceCode").
    /// </param>
    public static IReadOnlyList<PayloadDiff> Compare(
        string? oursJson,
        string? theirsJson,
        IEnumerable<string> ignorePathFragments)
    {
        if (string.IsNullOrWhiteSpace(oursJson) || string.IsNullOrWhiteSpace(theirsJson))
        {
            return Array.Empty<PayloadDiff>();
        }

        var ours = Flatten(oursJson);
        var theirs = Flatten(theirsJson);
        if (ours == null || theirs == null)
        {
            return Array.Empty<PayloadDiff>();
        }

        var ignore = ignorePathFragments?
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToArray() ?? Array.Empty<string>();

        bool Skipped(string path) => ignore.Any(f => path.Contains(f, StringComparison.OrdinalIgnoreCase));

        var diffs = new List<PayloadDiff>();

        foreach (var kv in ours)
        {
            if (Skipped(kv.Key))
            {
                continue;
            }

            if (!theirs.TryGetValue(kv.Key, out string? theirValue))
            {
                diffs.Add(new PayloadDiff { Kind = PayloadDiffKind.MissingInTheirs, Path = kv.Key, Ours = kv.Value });
            }
            else if (!string.Equals(Normalize(kv.Value), Normalize(theirValue), StringComparison.Ordinal))
            {
                diffs.Add(new PayloadDiff
                {
                    Kind = PayloadDiffKind.ValueDiffers,
                    Path = kv.Key,
                    Ours = kv.Value,
                    Theirs = theirValue
                });
            }
        }

        foreach (var kv in theirs)
        {
            if (Skipped(kv.Key) || ours.ContainsKey(kv.Key))
            {
                continue;
            }

            diffs.Add(new PayloadDiff { Kind = PayloadDiffKind.MissingInOurs, Path = kv.Key, Theirs = kv.Value });
        }

        return diffs
            .OrderBy(d => d.Kind)
            .ThenBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Normalize(string value)
        => (value ?? string.Empty).Trim().Trim('"');

    private static Dictionary<string, string>? Flatten(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Flatten(doc.RootElement, string.Empty, map);
            return map;
        }
        catch
        {
            return null;
        }
    }

    private static void Flatten(JsonElement element, string path, Dictionary<string, string> map)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    string childPath = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                    Flatten(property.Value, childPath, map);
                }

                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, $"{path}[{index}]", map);
                    index++;
                }

                break;

            default:
                map[path] = element.ValueKind == JsonValueKind.String
                    ? element.GetString() ?? string.Empty
                    : element.GetRawText();
                break;
        }
    }
}
