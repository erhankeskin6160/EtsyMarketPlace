namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class EtsyLogisticsService : IEtsyLogisticsService
{
    private readonly IEtsyLogisticsApiClient _apiClient;
    private static readonly ConcurrentDictionary<string, List<EtsyTaxonomyNodeItemDto>> TaxonomyCache = new();

    public EtsyLogisticsService(IEtsyLogisticsApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public async Task<IReadOnlyList<EtsyShippingProfileDto>> GetShippingProfilesAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return [];

        var json = await _apiClient.GetRawShippingProfilesJsonAsync(shopId, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        var list = new List<EtsyShippingProfileDto>();

        if (doc.RootElement.TryGetProperty("results", out var rArray) && rArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rArray.EnumerateArray())
            {
                if (item.TryGetProperty("shipping_profile_id", out var idProp))
                {
                    var id = idProp.GetInt64();
                    var title = item.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? $"Profil #{id}" : $"Profil #{id}";
                    int? minDays = item.TryGetProperty("min_processing_days", out var minProp) && minProp.TryGetInt32(out var minVal) ? minVal : null;
                    int? maxDays = item.TryGetProperty("max_processing_days", out var maxProp) && maxProp.TryGetInt32(out var maxVal) ? maxVal : null;

                    string? display = (minDays.HasValue && maxDays.HasValue) ? $"{minDays}-{maxDays} iş günü" : null;
                    list.Add(new EtsyShippingProfileDto(id, title, minDays, maxDays, display));
                }
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<EtsyReadinessStateDto>> GetReadinessStatesAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return [];

        var list = new List<EtsyReadinessStateDto>();

        // 1. Resmi definitions endpoint'i dene
        try
        {
            var defJson = await _apiClient.GetRawReadinessDefinitionsJsonAsync(shopId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(defJson))
            {
                using var defDoc = JsonDocument.Parse(defJson);
                if (defDoc.RootElement.TryGetProperty("results", out var rArray) && rArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in rArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("readiness_state_id", out var idProp))
                        {
                            var id = idProp.GetInt64();
                            var state = item.TryGetProperty("readiness_state", out var sProp) ? sProp.GetString() : null;
                            var minTime = item.TryGetProperty("min_processing_time", out var minE) && minE.TryGetInt32(out var minVal) ? minVal : 0;
                            var maxTime = item.TryGetProperty("max_processing_time", out var maxE) && maxE.TryGetInt32(out var maxVal) ? maxVal : 0;
                            var unit = item.TryGetProperty("processing_time_unit", out var unitE) ? unitE.GetString() ?? "iş günü" : "iş günü";

                            string title;
                            if (!string.IsNullOrWhiteSpace(state))
                            {
                                var stateTr = state.Equals("made_to_order", StringComparison.OrdinalIgnoreCase)
                                    ? "Siparişe Özel (Made to order)"
                                    : "Hazır Ürün (Ready to ship)";
                                title = (minTime > 0 || maxTime > 0) ? $"{stateTr} ({minTime}-{maxTime} {unit})" : stateTr;
                            }
                            else
                            {
                                title = $"Hazırlık Durumu #{id}";
                            }

                            list.Add(new EtsyReadinessStateDto(id, title, state, minTime, maxTime, unit));
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Fallback: Aktif ilanlardan kullanılan ID'leri tara
        if (list.Count == 0)
        {
            try
            {
                var actJson = await _apiClient.GetRawActiveListingsJsonAsync(shopId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(actJson))
                {
                    using var actDoc = JsonDocument.Parse(actJson);
                    if (actDoc.RootElement.TryGetProperty("results", out var aListings) && aListings.ValueKind == JsonValueKind.Array)
                    {
                        var seenIds = new HashSet<long>();
                        foreach (var al in aListings.EnumerateArray())
                        {
                            if (al.TryGetProperty("readiness_state_id", out var rsProp) && rsProp.GetInt64() > 0)
                            {
                                var id = rsProp.GetInt64();
                                if (seenIds.Add(id))
                                {
                                    list.Add(new EtsyReadinessStateDto(
                                        id,
                                        $"Hazırlık Durumu #{id}",
                                        "ready_to_ship",
                                        1,
                                        3,
                                        "iş günü"
                                    ));
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        return list;
    }

    public async Task<IReadOnlyList<EtsyTaxonomyNodeItemDto>> GetSellerTaxonomyNodesAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        if (TaxonomyCache.TryGetValue("all", out var cached) && cached.Count > 0)
        {
            return cached;
        }

        var json = await _apiClient.GetRawSellerTaxonomyNodesJsonAsync(shopId, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return [];

        using var doc = JsonDocument.Parse(json);
        var list = new List<EtsyTaxonomyNodeItemDto>();

        if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var rootNode in results.EnumerateArray())
            {
                FlattenTaxonomyTree(rootNode, new List<string>(), list);
            }
        }

        TaxonomyCache["all"] = list;
        return list;
    }

    private static void FlattenTaxonomyTree(JsonElement node, List<string> parentPath, List<EtsyTaxonomyNodeItemDto> collector)
    {
        long id = 0;
        if (node.TryGetProperty("id", out var idProp)) id = idProp.GetInt64();
        else if (node.TryGetProperty("taxonomy_id", out var tProp)) id = tProp.GetInt64();

        string name = "";
        if (node.TryGetProperty("name", out var nProp)) name = nProp.GetString() ?? "";

        var currentPath = new List<string>(parentPath);
        if (!string.IsNullOrWhiteSpace(name)) currentPath.Add(name);

        if (id > 0 && currentPath.Count > 0)
        {
            collector.Add(new EtsyTaxonomyNodeItemDto(id, name, string.Join(" > ", currentPath)));
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                FlattenTaxonomyTree(child, currentPath, collector);
            }
        }
    }
}
