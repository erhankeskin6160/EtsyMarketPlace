namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;

public sealed class EtsyListingService : IEtsyListingService
{
    private static readonly ConcurrentDictionary<string, string> ListingPrimaryImgCache = new();
    private static readonly string[] TrademarkTerms =
    [
        "ben 10", "omnitrix", "thor", "mjolnir", "marvel", "disney", "valorant", "kratos",
        "god of war", "pokemon", "nintendo", "star wars", "harry potter", "demon slayer",
        "naruto", "minecraft", "batman", "spiderman", "spider-man", "superman", "iron man",
        "captain america", "hulk"
    ];

    private readonly IEtsyListingApiClient _listingClient;
    private readonly IShopSettingsRepository _settingsRepo;

    public EtsyListingService(
        IEtsyListingApiClient listingClient,
        IShopSettingsRepository settingsRepo)
    {
        _listingClient = listingClient ?? throw new ArgumentNullException(nameof(listingClient));
        _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));
    }

    public async Task<IReadOnlyList<ShopListingItemDto>> GetShopListingsAsync(
        string shopId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rawJson = await _listingClient.GetShopListingsRawJsonAsync(shopId, limit, cancellationToken);
        using var doc = JsonDocument.Parse(rawJson);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ShopListingItemDto>();
        }

        var savedAudits = (await _settingsRepo.GetListingAuditsAsync(shopId, cancellationToken))
            .ToDictionary(a => a.ListingId, a => a);

        var items = new List<ShopListingItemDto>();
        int rank = 1;

        foreach (var item in results.EnumerateArray())
        {
            var listingId = item.TryGetProperty("listing_id", out var lid) ? lid.GetInt64().ToString() : "";
            var title = item.TryGetProperty("title", out var t) ? (t.GetString() ?? "") : "";
            var description = item.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";
            var views = item.TryGetProperty("views", out var v) ? v.GetInt32() : 0;
            int favorites = 0;
            if (item.TryGetProperty("num_favorers", out var f))
            {
                favorites = f.ValueKind == JsonValueKind.Number ? f.GetInt32() : (int.TryParse(f.GetString(), out var nf) ? nf : 0);
            }
            else if (item.TryGetProperty("favorites", out var favProp))
            {
                favorites = favProp.ValueKind == JsonValueKind.Number ? favProp.GetInt32() : (int.TryParse(favProp.GetString(), out var nf) ? nf : 0);
            }
            var quantity = item.TryGetProperty("quantity", out var q) ? q.GetInt32() : 0;

            decimal price = 0;
            string currency = "USD";
            if (item.TryGetProperty("price", out var pObj) && pObj.ValueKind == JsonValueKind.Object)
            {
                if (pObj.TryGetProperty("amount", out var aProp) && pObj.TryGetProperty("divisor", out var divProp))
                {
                    var divisor = divProp.GetInt32();
                    if (divisor > 0) price = aProp.GetInt64() / (decimal)divisor;
                }
                if (pObj.TryGetProperty("currency_code", out var cProp)) currency = cProp.GetString() ?? "USD";
            }

            var tags = new List<string>();
            if (item.TryGetProperty("tags", out var tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var tg in tagsArr.EnumerateArray())
                {
                    var str = tg.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) tags.Add(str.Trim());
                }
            }

            var materials = new List<string>();
            if (item.TryGetProperty("materials", out var matArr) && matArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var mt in matArr.EnumerateArray())
                {
                    var str = mt.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) materials.Add(str.Trim());
                }
            }

            var imageUrls = new List<string>();
            var imgProp = (item.TryGetProperty("Images", out var p1) && p1.ValueKind == JsonValueKind.Array) ? p1
                : ((item.TryGetProperty("images", out var p2) && p2.ValueKind == JsonValueKind.Array) ? p2 : default);

            if (imgProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imgProp.EnumerateArray())
                {
                    string? imgUrl = null;
                    if (img.TryGetProperty("url_570xN", out var u570)) imgUrl = u570.GetString();
                    else if (img.TryGetProperty("url_fullxfull", out var uFull)) imgUrl = uFull.GetString();
                    else if (img.TryGetProperty("url_170x135", out var u170)) imgUrl = u170.GetString();
                    else if (img.TryGetProperty("url_75x75", out var u75)) imgUrl = u75.GetString();

                    if (!string.IsNullOrWhiteSpace(imgUrl)) imageUrls.Add(imgUrl.Trim());
                }
            }
            var primaryImg = imageUrls.FirstOrDefault() ?? "";

            if (string.IsNullOrWhiteSpace(primaryImg) && !string.IsNullOrWhiteSpace(listingId) && ListingPrimaryImgCache.TryGetValue(listingId, out var cachedImg))
            {
                primaryImg = cachedImg;
                imageUrls.Add(cachedImg);
            }

            if (string.IsNullOrWhiteSpace(primaryImg) && !string.IsNullOrWhiteSpace(listingId) && long.TryParse(listingId, out var lidVal) && lidVal > 0)
            {
                try
                {
                    var imgJson = await _listingClient.GetListingImagesRawJsonAsync(lidVal, shopId, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(imgJson))
                    {
                        using var imgDoc = JsonDocument.Parse(imgJson);
                        if (imgDoc.RootElement.TryGetProperty("results", out var imgArr) && imgArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var img in imgArr.EnumerateArray())
                            {
                                string? urlFound = null;
                                if (img.TryGetProperty("url_570xN", out var u570)) urlFound = u570.GetString();
                                else if (img.TryGetProperty("url_fullxfull", out var uFull)) urlFound = uFull.GetString();
                                else if (img.TryGetProperty("url_170x135", out var u170)) urlFound = u170.GetString();
                                else if (img.TryGetProperty("url_75x75", out var u75)) urlFound = u75.GetString();

                                if (!string.IsNullOrWhiteSpace(urlFound))
                                {
                                    primaryImg = urlFound.Trim();
                                    imageUrls.Add(primaryImg);
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(primaryImg) && !string.IsNullOrWhiteSpace(listingId))
            {
                ListingPrimaryImgCache[listingId] = primaryImg;
            }

            // SEO Analizi
            int seoScore = 100;
            var needs = new List<string>();
            var strengths = new List<string>();

            if (tags.Count < 13)
            {
                var missingTags = 13 - tags.Count;
                seoScore -= missingTags * 4;
                needs.Add($"• Eksik Tag: {missingTags} tag eksik (13/13 etiket hakkının tümü kullanılmalı).");
            }
            else strengths.Add("13 tag eksiksiz");

            if (title.Length < 60)
            {
                seoScore -= 15;
                needs.Add($"• Başlık Çok Kısa: {title.Length}/140 karakter ({140 - title.Length} karakter boş bırakılmış).");
            }
            else if (title.Length < 110)
            {
                seoScore -= 6;
                needs.Add($"• Başlık Alanı İsrafı: {title.Length}/140 karakter ({140 - title.Length} karakter daha kullanılabilir).");
            }
            else if (title.Length <= 140) strengths.Add("Başlık uzunluğu ideal");

            if (description.Length < 500)
            {
                seoScore -= 10;
                needs.Add($"• Açıklama Kısa: {description.Length} karakter (en az 500 karakter detaylı hikaye ve özellik önerilir).");
            }
            else strengths.Add("Açıklama zengin");

            if (imageUrls.Count < 5)
            {
                seoScore -= 10;
                needs.Add($"• Görsel Az: {imageUrls.Count} görsel (Etsy listelemesinde en az 5-10 görsel önerilir).");
            }
            else strengths.Add("Görsel sayısı yeterli");

            seoScore = Math.Clamp(seoScore, 10, 100);

            var riskBlob = $"{title} {description} {string.Join(' ', tags)}".ToLowerInvariant();
            var detectedRisks = new List<string>();
            foreach (var term in TrademarkTerms)
            {
                if (riskBlob.Contains(term))
                {
                    detectedRisks.Add($"🚨 Telif ve Marka Riski: '{term}' tescilli markadır (IP/Trademark). Hak sahipleri veya Etsy tarafından telif yaptırımı riski taşır.");
                }
            }

            bool isAiAudited = false;
            int? aiScore = null;
            string status = detectedRisks.Count > 0 ? "⚠️ AI: Risk Var" : "Bekliyor";
            string? resultJson = null;
            object? savedAuditObj = null;

            if (savedAudits.TryGetValue(listingId, out var saved))
            {
                isAiAudited = true;
                aiScore = saved.OptimizedSeoScore;
                status = saved.Status;
                resultJson = saved.ResultJson;
                savedAuditObj = new
                {
                    shopId = saved.ShopId,
                    listingId = saved.ListingId,
                    title = saved.Title,
                    currentSeoScore = saved.CurrentSeoScore,
                    optimizedSeoScore = saved.OptimizedSeoScore,
                    seoScoreBefore = saved.CurrentSeoScore,
                    seoScoreAfter = saved.OptimizedSeoScore,
                    status = saved.Status,
                    provider = saved.Provider,
                    model = saved.Model,
                    aiModel = saved.Model,
                    resultJson = saved.ResultJson,
                    auditedAt = saved.AuditedAt
                };
            }

            items.Add(new ShopListingItemDto(
                Rank: rank++,
                ListingId: listingId,
                Title: title,
                Description: description,
                Tags: tags,
                Materials: materials,
                Price: price,
                PriceAmount: price,
                Currency: currency,
                CurrencyCode: currency,
                Quantity: quantity,
                Views: views,
                Favorites: favorites,
                NumFavorers: favorites,
                Images: imageUrls,
                Thumbnail: primaryImg,
                PrimaryImageUrl: primaryImg,
                SeoScore: seoScore,
                AiScore: aiScore,
                Status: status,
                IsAiAudited: isAiAudited,
                ResultJson: resultJson,
                SeoNeeds: string.Join("\n", needs),
                SeoStrengths: string.Join(", ", strengths),
                StructuralNeeds: needs,
                RiskWarnings: detectedRisks,
                HasSavedAudit: isAiAudited,
                SavedAudit: savedAuditObj));
        }

        return items;
    }

    public async Task<ListingUpdateResultDto> UpdateListingAsync(
        string shopId,
        string listingId,
        UpdateListingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 140)
            throw new ArgumentException("Başlık 1 ile 140 karakter arasında olmalıdır.");

        if (request.Title.Count(c => c == '&') > 1)
            throw new ArgumentException("Etsy kuralı: Başlıkta '&' karakteri en fazla 1 kez kullanılabilir.");

        var formDict = new Dictionary<string, string>
        {
            ["title"] = request.Title.Trim(),
            ["description"] = (request.Description ?? string.Empty).Trim()
        };

        var validTags = (request.Tags ?? Array.Empty<string>())
            .Select(t => t.Trim())
            .Where(t => t.Length > 0 && t.Length <= 20)
            .Take(13)
            .ToList();

        if (validTags.Count > 0)
        {
            formDict["tags"] = string.Join(",", validTags);
        }

        var (success, body, error) = await _listingClient.UpdateListingRawAsync(shopId, listingId, formDict, cancellationToken);
        if (!success)
        {
            throw new InvalidOperationException(error ?? "Etsy güncelleme başarısız.");
        }

        await _settingsRepo.SaveListingAuditAsync(new SaveListingAuditRecordRequest(
            shopId,
            listingId,
            request.Title,
            100,
            100,
            "🚀 Etsy güncellendi",
            "EtsyAPI",
            "DirectPush",
            body), cancellationToken);

        return new ListingUpdateResultDto(true, listingId, "Listing Etsy'de başarıyla güncellendi!");
    }

    public async Task<ListingUpdateResultDto> UpdateListingStateAsync(
        string shopId,
        long listingId,
        string state,
        CancellationToken cancellationToken = default)
    {
        var targetState = state?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(targetState) || (targetState != "active" && targetState != "draft" && targetState != "inactive"))
        {
            throw new ArgumentException("Geçersiz durum. Sadece 'active', 'draft' veya 'inactive' kabul edilir.");
        }

        var success = await _listingClient.SetListingStateAsync(shopId, listingId, targetState, cancellationToken);
        if (!success)
        {
            throw new InvalidOperationException($"Etsy ilan durumu '{targetState}' olarak güncellenemedi.");
        }

        return new ListingUpdateResultDto(true, listingId.ToString(), $"İlan durumu başarıyla '{targetState}' olarak güncellendi.", targetState);
    }

    public async Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(
        string shopId,
        CancellationToken cancellationToken = default)
    {
        return await _settingsRepo.GetListingAuditsAsync(shopId, cancellationToken);
    }
}
