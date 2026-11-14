namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Taxonomy;
using EtsyMarketPlace.Domain.Taxonomy;

public sealed class EtsyLogisticsService : IEtsyLogisticsService
{
    private readonly IEtsyLogisticsApiClient _apiClient;
    private readonly IEtsyTaxonomyRepository? _taxonomyRepository;
    private static readonly ConcurrentDictionary<string, List<EtsyTaxonomyNodeItemDto>> TaxonomyCache = new();
    private static readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, IReadOnlyList<EtsyShippingProfileDto> Profiles)> ShippingProfileCache = new();
    private static readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, IReadOnlyList<EtsyReturnPolicyDto> Policies)> ReturnPolicyCache = new();

    public EtsyLogisticsService(
        IEtsyLogisticsApiClient apiClient,
        IEtsyTaxonomyRepository? taxonomyRepository = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _taxonomyRepository = taxonomyRepository;
    }

    public async Task<IReadOnlyList<EtsyShippingProfileDto>> GetShippingProfilesAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return [];

        if (ShippingProfileCache.TryGetValue(shopId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Profiles;
        }

        string? json;
        try
        {
            json = await _apiClient.GetRawShippingProfilesJsonAsync(shopId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return cached.Profiles ?? Array.Empty<EtsyShippingProfileDto>();
        }

        if (string.IsNullOrWhiteSpace(json)) return cached.Profiles ?? Array.Empty<EtsyShippingProfileDto>();

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

        ShippingProfileCache[shopId] = (DateTimeOffset.UtcNow.AddMinutes(10), list);
        return list;
    }

    public async Task<IReadOnlyList<EtsyReadinessStateDto>> GetReadinessStatesAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return [];

        var list = new List<EtsyReadinessStateDto>();

        // 1. Resmi API: GET /v3/application/shops/{shop_id}/readiness-state-definitions
        try
        {
            var json = await _apiClient.GetRawReadinessDefinitionsJsonAsync(shopId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("results", out var rArray) && rArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in rArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("readiness_state_id", out var idProp))
                        {
                            var id = idProp.GetInt64();
                            string? title = item.TryGetProperty("title", out var tProp) ? tProp.GetString() : null;
                            string? state = item.TryGetProperty("readiness_state", out var sProp) ? sProp.GetString() : null;
                            int minTime = item.TryGetProperty("min_processing_time", out var minP) && minP.TryGetInt32(out var minV) ? minV : 0;
                            int maxTime = item.TryGetProperty("max_processing_time", out var maxP) && maxP.TryGetInt32(out var maxV) ? maxV : 0;
                            string unit = item.TryGetProperty("processing_time_unit", out var uP) ? uP.GetString() ?? "iş günü" : "iş günü";

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

    public async Task<IReadOnlyList<EtsyReturnPolicyDto>> GetReturnPoliciesAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId)) return [];
        var normalizedShopId = shopId.Trim();

        if (ReturnPolicyCache.TryGetValue(normalizedShopId, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Policies;
        }

        var list = new List<EtsyReturnPolicyDto>();

        // 1. Resmi API: GET /v3/application/shops/{shop_id}/policies/return
        try
        {
            var json = await _apiClient.GetRawReturnPoliciesJsonAsync(normalizedShopId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("results", out var rArray) && rArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in rArray.EnumerateArray())
                    {
                        if (TryParseReturnPolicyElement(item, out var policy) && policy != null)
                        {
                            list.Add(policy);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch { }

        // 2. Fallback: Aktif ilanlardan kullanılan return_policy_id değerlerini tara
        if (list.Count == 0)
        {
            list.AddRange(await ExtractReturnPoliciesFromActiveListingsAsync(normalizedShopId, cancellationToken));
        }

        if (list.Count > 0)
        {
            var ordered = list
                .OrderByDescending(p => p.AcceptsReturns)
                .ThenBy(p => p.ReturnPolicyId)
                .ToList();
            ReturnPolicyCache[normalizedShopId] = (DateTimeOffset.UtcNow.AddMinutes(10), ordered);
            return ordered;
        }

        return cached.Policies ?? [];
    }

    public async Task<(bool Success, EtsyReturnPolicyDto? Policy, string? ErrorMessage)> CreateReturnPolicyAsync(
        CreateEtsyReturnPolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.ShopId))
        {
            return (false, null, "Mağaza kimliği (Shop ID) belirtilmelidir.");
        }

        var requiresDeadline = command.AcceptsReturns || command.AcceptsExchanges;
        if (requiresDeadline && (!command.ReturnDeadline.HasValue || command.ReturnDeadline.Value <= 0))
        {
            return (false, null, "İade veya değişim kabul edildiğinde geçerli bir iade süresi (ör. 14, 30 gün) seçilmelidir.");
        }

        var normalizedShopId = command.ShopId.Trim();
        int? effectiveDeadline = requiresDeadline ? command.ReturnDeadline : null;

        var (success, responseJson, errorMessage) = await _apiClient.CreateReturnPolicyRawAsync(
            normalizedShopId,
            command.AcceptsReturns,
            command.AcceptsExchanges,
            effectiveDeadline,
            cancellationToken);

        if (!success)
        {
            return (false, null, errorMessage ?? "Etsy iade politikası oluşturulamadı.");
        }

        EtsyReturnPolicyDto? createdPolicy = null;
        if (!string.IsNullOrWhiteSpace(responseJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                if (!TryParseReturnPolicyElement(doc.RootElement, out createdPolicy) &&
                    doc.RootElement.TryGetProperty("results", out var rArray) &&
                    rArray.ValueKind == JsonValueKind.Array &&
                    rArray.GetArrayLength() > 0)
                {
                    TryParseReturnPolicyElement(rArray[0], out createdPolicy);
                }
            }
            catch
            {
                createdPolicy = null;
            }
        }

        if (createdPolicy == null)
        {
            return (false, null, "Etsy iade politikası oluşturuldu ancak yanıt ayrıştırılamadı.");
        }

        ReturnPolicyCache.TryRemove(normalizedShopId, out _);
        return (true, createdPolicy, null);
    }

    public static string FormatReturnPolicyTitle(bool acceptsReturns, bool acceptsExchanges, int? returnDeadline)
    {
        var daysText = returnDeadline.HasValue && returnDeadline.Value > 0 ? $" ({returnDeadline.Value} Gün)" : string.Empty;
        if (acceptsReturns && acceptsExchanges) return $"İade ve Değişim Kabul Edilir{daysText}";
        if (acceptsReturns) return $"Yalnızca İade Kabul Edilir{daysText}";
        if (acceptsExchanges) return $"Yalnızca Değişim Kabul Edilir{daysText}";
        return "İade ve Değişim Kabul Edilmez";
    }

    private async Task<List<EtsyReturnPolicyDto>> ExtractReturnPoliciesFromActiveListingsAsync(
        string shopId,
        CancellationToken cancellationToken)
    {
        var fallbackList = new List<EtsyReturnPolicyDto>();
        try
        {
            var actJson = await _apiClient.GetRawActiveListingsJsonAsync(shopId, cancellationToken);
            if (string.IsNullOrWhiteSpace(actJson)) return fallbackList;

            using var actDoc = JsonDocument.Parse(actJson);
            if (!actDoc.RootElement.TryGetProperty("results", out var aListings) || aListings.ValueKind != JsonValueKind.Array)
                return fallbackList;

            var seenIds = new HashSet<long>();
            foreach (var al in aListings.EnumerateArray())
            {
                if (al.TryGetProperty("return_policy_id", out var rpProp) &&
                    rpProp.ValueKind == JsonValueKind.Number &&
                    rpProp.TryGetInt64(out var id) &&
                    id > 0 &&
                    seenIds.Add(id))
                {
                    fallbackList.Add(new EtsyReturnPolicyDto(
                        ReturnPolicyId: id,
                        AcceptsReturns: true,
                        AcceptsExchanges: true,
                        ReturnDeadline: 30,
                        Title: $"Mevcut İade Politikası (#{id})"
                    ));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch { }

        return fallbackList;
    }

    private static bool TryParseReturnPolicyElement(JsonElement item, out EtsyReturnPolicyDto? policy)
    {
        policy = null;
        if (item.ValueKind != JsonValueKind.Object) return false;
        if (!item.TryGetProperty("return_policy_id", out var idProp) ||
            idProp.ValueKind != JsonValueKind.Number ||
            !idProp.TryGetInt64(out var id) ||
            id <= 0)
        {
            return false;
        }

        var acceptsReturns = item.TryGetProperty("accepts_returns", out var arProp) && arProp.ValueKind == JsonValueKind.True;
        var acceptsExchanges = item.TryGetProperty("accepts_exchanges", out var aeProp) && aeProp.ValueKind == JsonValueKind.True;
        int? deadline = item.TryGetProperty("return_deadline", out var rdProp) && rdProp.ValueKind == JsonValueKind.Number && rdProp.TryGetInt32(out var rdVal)
            ? rdVal
            : null;

        var title = FormatReturnPolicyTitle(acceptsReturns, acceptsExchanges, deadline);
        policy = new EtsyReturnPolicyDto(id, acceptsReturns, acceptsExchanges, deadline, title);
        return true;
    }

    public async Task<IReadOnlyList<EtsyTaxonomyNodeItemDto>> GetSellerTaxonomyNodesAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        if (TaxonomyCache.TryGetValue("all", out var cached) && cached.Count > 0)
        {
            return cached;
        }

        // 1. Önce yerel SQLite veritabanından oku (Sıfır gecikme & offline dayanıklılık)
        if (_taxonomyRepository != null)
        {
            try
            {
                var dbNodes = await _taxonomyRepository.GetAllAsync(cancellationToken);
                if (dbNodes.Count > 0)
                {
                    var mapped = dbNodes.Select(n => new EtsyTaxonomyNodeItemDto(n.Id, n.Name, n.Path)).ToList();
                    TaxonomyCache["all"] = mapped;
                    return mapped;
                }
            }
            catch
            {
                // DB okuma hatası olursa canlı API çağrısına düş
            }
        }

        // 2. Canlı Etsy API: GET /v3/application/seller-taxonomy/nodes
        string? json = null;
        try
        {
            json = await _apiClient.GetRawSellerTaxonomyNodesJsonAsync(shopId, cancellationToken);
        }
        catch
        {
            // API çağrısı başarısız olursa (örn. offline veya yetkisiz), sessizce yakala
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return TaxonomyCache.TryGetValue("all", out var inMem) ? inMem : [];
        }

        using var doc = JsonDocument.Parse(json);
        var list = new List<EtsyTaxonomyNodeItemDto>();
        var domainList = new List<EtsyTaxonomyNode>();

        if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var rootNode in results.EnumerateArray())
            {
                FlattenTaxonomyTree(rootNode, new List<string>(), 0, 0, list, domainList);
            }
        }

        // 3. Çekilen 3.065 kategoriyi SQLite veritabanına tek işlemde kalıcı kaydet
        if (_taxonomyRepository != null && domainList.Count > 0)
        {
            try
            {
                await _taxonomyRepository.SaveAllAsync(domainList, cancellationToken);
            }
            catch
            {
                // Veritabanı yazma hatası kritik akışı engellemez
            }
        }

        TaxonomyCache["all"] = list;
        return list;
    }

    public async Task<IReadOnlyList<EtsyTaxonomyNodeItemDto>> SearchSellerTaxonomyNodesAsync(
        string shopId,
        string query,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // 1. Önce doğrudan SQLite veritabanında ara (en hızlı ve güvenli)
        if (_taxonomyRepository != null)
        {
            try
            {
                var dbResults = await _taxonomyRepository.SearchAsync(query, limit, cancellationToken);
                if (dbResults.Count > 0)
                {
                    return dbResults.Select(n => new EtsyTaxonomyNodeItemDto(n.Id, n.Name, n.Path)).ToList();
                }
            }
            catch
            {
                // Fallback
            }
        }

        // 2. SQLite boş veya sonuçsuz ise ağacı çekmeyi / tohumlamayı dene
        await GetSellerTaxonomyNodesAsync(shopId, cancellationToken);

        if (_taxonomyRepository != null)
        {
            try
            {
                var dbResults = await _taxonomyRepository.SearchAsync(query, limit, cancellationToken);
                if (dbResults.Count > 0)
                {
                    return dbResults.Select(n => new EtsyTaxonomyNodeItemDto(n.Id, n.Name, n.Path)).ToList();
                }
            }
            catch
            {
                // Fallback to cache search
            }
        }

        if (TaxonomyCache.TryGetValue("all", out var cached))
        {
            var q = (query ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(q)) return cached.Take(limit).ToList();
            return cached.Where(c => c.Path.ToLowerInvariant().Contains(q) || c.Name.ToLowerInvariant().Contains(q)).Take(limit).ToList();
        }

        return [];
    }

    private static void FlattenTaxonomyTree(
        JsonElement node,
        List<string> parentPath,
        long parentId,
        int level,
        List<EtsyTaxonomyNodeItemDto> collector,
        List<EtsyTaxonomyNode> domainCollector)
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
            var fullPath = string.Join(" > ", currentPath);
            collector.Add(new EtsyTaxonomyNodeItemDto(id, name, fullPath));
            domainCollector.Add(new EtsyTaxonomyNode(id, name, fullPath, parentId, level));
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                FlattenTaxonomyTree(child, currentPath, id, level + 1, collector, domainCollector);
            }
        }
    }
}
