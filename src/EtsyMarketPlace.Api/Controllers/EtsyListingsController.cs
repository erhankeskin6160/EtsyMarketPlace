using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy")]
public class EtsyListingsController : BaseApiController
{
    private static readonly ConcurrentDictionary<string, string> ListingPrimaryImgCache = new();

    private readonly IConfiguration _config;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEtsyOAuthService _oauthService;
    private readonly ListingOptimizationService _optimizer;

    public EtsyListingsController(
        IConfiguration config,
        IEtsyTokenStore tokenStore,
        IShopSettingsRepository settingsRepo,
        IHttpClientFactory httpClientFactory,
        IEtsyOAuthService oauthService,
        ListingOptimizationService optimizer)
    {
        _config = config;
        _tokenStore = tokenStore;
        _settingsRepo = settingsRepo;
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
        _optimizer = optimizer;
    }

    [HttpGet("shop/listings")]
    [EndpointSummary("Mağazanın Aktif Listinglerini ve Yapısal SEO Analizini Getir")]
    public async Task<IActionResult> GetShopListings([FromQuery] string shopId = "53236321", [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token is null)
            return NotFound(new { error = "Bu mağaza için kayıtlı OAuth token bulunamadı." });

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            try
            {
                token = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
                await _tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Etsy token yenilenemedi: " + ex.Message });
            }
        }

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        if (string.IsNullOrWhiteSpace(keystring))
            return BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin." });
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var client = _httpClientFactory.CreateClient();
        var clampedLimit = Math.Clamp(limit, 1, 100);
        var url = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings?state=active&limit={clampedLimit}&sort_on=updated&sort_order=desc&includes=Images";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKeyHeader);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        string body;
        if (!response.IsSuccessStatusCode)
        {
            var fallbackUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/active?limit={clampedLimit}&sort_on=updated&sort_order=desc";
            using var fbReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
            fbReq.Headers.Add("x-api-key", apiKeyHeader);
            fbReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
            using var fbResp = await client.SendAsync(fbReq, cancellationToken);
            if (!fbResp.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                return BadRequest(new { error = $"Etsy API hatası (HTTP {(int)response.StatusCode}): {err}" });
            }
            body = await fbResp.Content.ReadAsStringAsync(cancellationToken);
        }
        else
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return Ok(Array.Empty<object>());

        var savedAudits = (await _settingsRepo.GetListingAuditsAsync(resolvedShopId, cancellationToken))
            .ToDictionary(a => a.ListingId, a => a);

        var items = new List<object>();
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
                    var imgReqUrl = $"https://api.etsy.com/v3/application/listings/{lidVal}/images";
                    using var imgReq = new HttpRequestMessage(HttpMethod.Get, imgReqUrl);
                    imgReq.Headers.Add("x-api-key", apiKeyHeader);
                    using var imgResp = await client.SendAsync(imgReq, cancellationToken);
                    if (imgResp.IsSuccessStatusCode)
                    {
                        var imgBody = await imgResp.Content.ReadAsStringAsync(cancellationToken);
                        using var imgDoc = JsonDocument.Parse(imgBody);
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

            // Structural SEO Analysis
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
            string[] trademarkTerms = ["ben 10", "omnitrix", "thor", "mjolnir", "marvel", "disney", "valorant", "kratos", "god of war", "pokemon", "nintendo", "star wars", "harry potter", "demon slayer", "naruto", "minecraft", "batman", "spiderman", "spider-man", "superman", "iron man", "captain america", "hulk"];
            foreach (var term in trademarkTerms)
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

            items.Add(new
            {
                rank = rank++,
                listingId,
                title,
                description,
                tags,
                materials,
                price,
                priceAmount = price,
                currency,
                currencyCode = currency,
                quantity,
                views,
                favorites,
                numFavorers = favorites,
                images = imageUrls,
                thumbnail = primaryImg,
                primaryImageUrl = primaryImg,
                seoScore,
                aiScore,
                status,
                isAiAudited,
                resultJson,
                seoNeeds = string.Join("\n", needs),
                seoStrengths = string.Join(", ", strengths),
                structuralNeeds = needs,
                riskWarnings = detectedRisks,
                hasSavedAudit = isAiAudited,
                savedAudit = savedAuditObj
            });
        }

        return Ok(items);
    }

    [HttpPost("listings/{listingId}/ai-optimize")]
    [EndpointSummary("Listing İçin Yapay Zeka SEO ve Başlık/Tag Optimizasyonu")]
    public async Task<IActionResult> AiOptimizeListing(
        string listingId,
        [FromQuery] string? shopId,
        [FromBody] OptimizeListingApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolvedShopId = !string.IsNullOrWhiteSpace(request?.ShopId)
                ? request.ShopId.Trim()
                : ResolveShopId(shopId, _config);

            var title = request?.Title ?? string.Empty;
            var description = request?.Description ?? string.Empty;
            var tags = request?.Tags ?? Array.Empty<string>();
            var targetKw = !string.IsNullOrWhiteSpace(request?.TargetKeyword)
                ? request.TargetKeyword
                : (!string.IsNullOrWhiteSpace(title) ? title : listingId);

            var input = new ListingOptimizationInput(title, description, tags, targetKw, request?.DescriptionStyle ?? "Storytelling");

            var apiKey = !string.IsNullOrWhiteSpace(request?.ApiKey)
                ? request.ApiKey.Trim()
                : Request.Headers.TryGetValue("X-Gemini-Api-Key", out var hKey) && !string.IsNullOrWhiteSpace(hKey)
                    ? hKey.ToString().Trim()
                    : _config["Gemini:ApiKey"] ?? string.Empty;

            var requestedProvider = request?.Provider?.Trim();
            var isOfflineRequested = string.Equals(requestedProvider, "Offline", StringComparison.OrdinalIgnoreCase);

            string resolvedProvider;
            string resolvedModel;
            string bestTitle;
            var titleSuggestions = new List<string>();
            var tagSuggestions = new List<string>();
            var materialSuggestions = new List<string>();
            string descriptionDraft;
            var riskWarnings = new List<string>();
            var checklist = new List<string>();
            int currentScore = 0;
            int optimizedScore = 0;
            string seoCritique = string.Empty;

            if (!isOfflineRequested && !string.IsNullOrWhiteSpace(apiKey))
            {
                var rawModel = !string.IsNullOrWhiteSpace(request?.Model) ? request.Model : "gemini-2.5-flash";
                var geminiModel = EtsyAiModelNormalizer.NormalizeGeminiTextModel(rawModel);

                var systemInstruction = ListingDraftInstructionBuilder.BuildSystemInstruction();
                var userPrompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(60);

                var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(geminiModel)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

                var geminiPayload = new
                {
                    systemInstruction = new
                    {
                        parts = new[] { new { text = systemInstruction } }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = userPrompt } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.35,
                        topP = 0.95,
                        maxOutputTokens = 4096,
                        responseMimeType = "application/json"
                    }
                };

                using var content = new StringContent(JsonSerializer.Serialize(geminiPayload), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(geminiUrl, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errMsg = $"Google Gemini API Hatası (HTTP {(int)response.StatusCode}): {response.ReasonPhrase}";
                    try
                    {
                        using var errDoc = JsonDocument.Parse(responseBody);
                        if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                            errObj.TryGetProperty("message", out var mObj))
                        {
                            errMsg = $"Google Gemini API Hatası: {mObj.GetString()}";
                        }
                    }
                    catch { }

                    return BadRequest(new { success = false, message = errMsg });
                }

                using var doc = JsonDocument.Parse(responseBody);
                var candidateText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(candidateText))
                {
                    return BadRequest(new { success = false, message = "Google Gemini boş yanıt döndürdü." });
                }

                using var parsedAi = JsonDocument.Parse(candidateText);
                var root = parsedAi.RootElement;

                if (root.TryGetProperty("title_suggestions", out var titlesEl) && titlesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in titlesEl.EnumerateArray())
                    {
                        var str = t.GetString();
                        if (!string.IsNullOrWhiteSpace(str)) titleSuggestions.Add(str.Trim());
                    }
                }

                if (root.TryGetProperty("tag_suggestions", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var t in tagsEl.EnumerateArray())
                    {
                        var str = t.GetString();
                        if (!string.IsNullOrWhiteSpace(str)) tagSuggestions.Add(str.Trim().ToLowerInvariant());
                    }
                }

                if (root.TryGetProperty("material_suggestions", out var matsEl) && matsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in matsEl.EnumerateArray())
                    {
                        var str = m.GetString();
                        if (!string.IsNullOrWhiteSpace(str)) materialSuggestions.Add(str.Trim());
                    }
                }

                if (root.TryGetProperty("description_draft", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                {
                    descriptionDraft = descEl.GetString()?.Trim() ?? string.Empty;
                }
                else
                {
                    descriptionDraft = string.Empty;
                }

                if (root.TryGetProperty("risk_warnings", out var risksEl) && risksEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in risksEl.EnumerateArray())
                    {
                        var str = r.GetString();
                        if (!string.IsNullOrWhiteSpace(str)) riskWarnings.Add(str.Trim());
                    }
                }

                if (root.TryGetProperty("current_seo_score", out var curScoreEl) && curScoreEl.TryGetInt32(out var cs))
                {
                    currentScore = cs;
                }
                if (root.TryGetProperty("optimized_seo_score", out var optScoreEl) && optScoreEl.TryGetInt32(out var os))
                {
                    optimizedScore = os;
                }
                if (root.TryGetProperty("seo_critique", out var critEl) && critEl.ValueKind == JsonValueKind.String)
                {
                    seoCritique = critEl.GetString()?.Trim() ?? string.Empty;
                }

                bestTitle = titleSuggestions.FirstOrDefault() ?? title;
                if (currentScore <= 0) currentScore = 80;
                if (optimizedScore <= 0) optimizedScore = 98;
                if (string.IsNullOrWhiteSpace(seoCritique))
                {
                    seoCritique = "Google Gemini AI ile başlık, 13 etiket ve ürün açıklaması Etsy arama algoritması için canlı olarak optimize edildi.";
                }

                checklist.Add($"SEO puanı {currentScore}/100 -> {optimizedScore}/100 seviyesine optimize edildi.");
                checklist.Add("13 Etsy etiket alanının tamamı Gemini ile dolduruldu.");
                checklist.Add("Açıklama satış odaklı ve Etsy SEO uyumlu olarak baştan yazıldı.");

                resolvedProvider = "Gemini (Canlı API)";
                resolvedModel = geminiModel;
            }
            else
            {
                if (!isOfflineRequested && !string.IsNullOrWhiteSpace(requestedProvider) && requestedProvider.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "⚠️ Canlı Gemini API anahtarı girilmemiş. Lütfen üst bardaki AI Ayarlarından Gemini API anahtarınızı kaydediniz veya Offline seçiniz." });
                }

                var offlineResult = _optimizer.Optimize(input);
                currentScore = offlineResult.CurrentSeoScore;
                optimizedScore = offlineResult.OptimizedSeoScore;
                titleSuggestions = offlineResult.TitleSuggestions.ToList();
                bestTitle = titleSuggestions.FirstOrDefault() ?? title;
                tagSuggestions = offlineResult.TagSuggestions.ToList();
                materialSuggestions = offlineResult.MaterialSuggestions.ToList();
                descriptionDraft = offlineResult.DescriptionDraft;
                riskWarnings = offlineResult.RiskWarnings.ToList();
                checklist = offlineResult.ActionChecklist.ToList();
                seoCritique = offlineResult.SeoCritique ?? string.Empty;
                resolvedProvider = "Offline (Kural Motoru)";
                resolvedModel = "RuleBased";
            }

            var status = riskWarnings.Count > 0 ? "⚠️ AI: Risk Var" : "✨ AI: Hazır";

            var auditData = new ListingOptimizationResult(
                currentScore,
                optimizedScore,
                titleSuggestions,
                tagSuggestions,
                materialSuggestions,
                descriptionDraft,
                Array.Empty<string>(),
                riskWarnings,
                checklist,
                ExecutedProvider: resolvedProvider,
                ExecutedModel: resolvedModel,
                IsFallback: resolvedProvider.StartsWith("Offline"),
                FallbackReason: null,
                SeoCritique: seoCritique);

            await _settingsRepo.SaveListingAuditAsync(new SaveListingAuditRecordRequest(
                resolvedShopId,
                listingId,
                title,
                currentScore,
                optimizedScore,
                status,
                resolvedProvider,
                resolvedModel,
                JsonSerializer.Serialize(auditData)), cancellationToken);

            return Ok(new
            {
                success = true,
                listingId,
                currentSeoScore = currentScore,
                optimizedSeoScore = optimizedScore,
                seoScoreBefore = currentScore,
                seoScoreAfter = optimizedScore,
                optimizedTitle = bestTitle,
                suggestedTitle = bestTitle,
                titleSuggestions,
                optimizedTags = tagSuggestions.Take(13).ToList(),
                tagSuggestions = tagSuggestions.Take(13).ToList(),
                materialSuggestions,
                optimizedDescription = descriptionDraft,
                descriptionDraft,
                critique = seoCritique,
                seoCritique,
                missingTerms = Array.Empty<string>(),
                riskWarnings,
                checklist,
                status,
                aiModel = resolvedModel,
                provider = resolvedProvider,
                model = resolvedModel,
                message = resolvedProvider.Contains("Canlı", StringComparison.OrdinalIgnoreCase)
                    ? "Listing başarıyla canlı Google Gemini AI ile optimize edildi ve yerel veritabanına kaydedildi."
                    : "Listing başarıyla yerel kural motoru ile optimize edildi ve yerel veritabanına kaydedildi."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"AI optimizasyon hatası: {ex.Message}" });
        }
    }

    [HttpPut("listings/{listingId}")]
    [EndpointSummary("İlanı Başlık, Etiket ve Açıklama ile Doğrudan Etsy'de Güncelle")]
    public async Task<IActionResult> UpdateListing(
        string listingId,
        [FromBody] UpdateListingApiRequest request,
        CancellationToken cancellationToken)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token is null)
            return NotFound(new { error = "Bu mağaza için kayıtlı OAuth token bulunamadı." });

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            try
            {
                token = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
                await _tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Etsy token yenilenemedi: " + ex.Message });
            }
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 140)
            return BadRequest(new { error = "Başlık 1 ile 140 karakter arasında olmalıdır." });

        if (request.Title.Count(c => c == '&') > 1)
            return BadRequest(new { error = "Etsy kuralı: Başlıkta '&' karakteri en fazla 1 kez kullanılabilir." });

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        if (string.IsNullOrWhiteSpace(keystring))
            return BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin." });
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var client = _httpClientFactory.CreateClient();
        var patchUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/{listingId}";

        var formDict = new Dictionary<string, string>
        {
            ["title"] = request.Title.Trim(),
            ["description"] = request.Description.Trim(),
        };

        var validTags = request.Tags.Select(t => t.Trim()).Where(t => t.Length > 0 && t.Length <= 20).Take(13).ToList();
        if (validTags.Count > 0)
        {
            formDict["tags"] = string.Join(",", validTags);
        }

        using var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
        {
            Content = new FormUrlEncodedContent(formDict)
        };
        patchReq.Headers.Add("x-api-key", apiKeyHeader);
        patchReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var patchRes = await client.SendAsync(patchReq, cancellationToken);
        var patchBody = await patchRes.Content.ReadAsStringAsync(cancellationToken);

        if (!patchRes.IsSuccessStatusCode)
        {
            return BadRequest(new { error = $"Etsy güncelleme başarısız (HTTP {(int)patchRes.StatusCode}): {patchBody}" });
        }

        await _settingsRepo.SaveListingAuditAsync(new SaveListingAuditRecordRequest(
            resolvedShopId,
            listingId,
            request.Title,
            100,
            100,
            "🚀 Etsy güncellendi",
            "EtsyAPI",
            "DirectPush",
            patchBody), cancellationToken);

        return Ok(new
        {
            success = true,
            listingId,
            message = "Listing Etsy'de başarıyla güncellendi!"
        });
    }

    [HttpPost("listings")]
    [EndpointSummary("Hızlı Ürün Ekleme (Fast Creator) İle Doğrudan Etsy'de Taslak/Canlı İlan Oluştur")]
    public async Task<IActionResult> CreateListing(
        [FromBody] CreateListingApiRequest request,
        [FromServices] IFastListingPublishService publishService,
        CancellationToken cancellationToken)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);
        var command = new FastListingPublishCommand(
            ShopId: resolvedShopId,
            Title: request.Title,
            Description: request.Description,
            Price: request.Price,
            Quantity: request.Quantity,
            TaxonomyId: request.TaxonomyId,
            ShippingProfileId: request.ShippingProfileId,
            ReadinessStateId: request.ReadinessStateId,
            IsDigital: request.IsDigital,
            Tags: request.Tags,
            Materials: request.Materials,
            State: request.State,
            WhoMade: request.WhoMade,
            WhenMade: request.WhenMade,
            Images: request.Images?.Select(i => new ListingImageInput(i.DataUrl, i.Url, i.Rank)).ToList(),
            Variations: request.Variations?.Select(v => new ListingVariationInput(v.Key, v.Price, v.Quantity, v.Active)).ToList(),
            VariationGroups: request.VariationGroups?.Select(g => new ListingVariationGroupInput(g.Name, g.Values)).ToList()
        );

        var result = await publishService.PublishListingAsync(command, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(result);
    }

    [HttpPatch("listings/{listingId:long}/state")]
    [EndpointSummary("Etsy İlan Durumunu Güncelle (draft / active / inactive)")]
    public async Task<IActionResult> UpdateListingState(
        long listingId,
        [FromBody] UpdateListingStateApiRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return Unauthorized(new { error = "Etsy mağaza yetkilendirmesi (Access Token) bulunamadı." });
        }

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            try
            {
                token = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
                await _tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Etsy token yenilenemedi: " + ex.Message });
            }
        }

        var targetState = request.State?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(targetState) || (targetState != "active" && targetState != "draft" && targetState != "inactive"))
        {
            return BadRequest(new { error = "Geçersiz durum. Sadece 'active', 'draft' veya 'inactive' kabul edilir." });
        }

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        if (string.IsNullOrWhiteSpace(keystring))
        {
            return BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı." });
        }
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var client = _httpClientFactory.CreateClient();
        var patchUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/{listingId}";
        var form = new List<KeyValuePair<string, string>>
        {
            new("state", targetState)
        };

        using var patchReq = new HttpRequestMessage(HttpMethod.Patch, patchUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };
        patchReq.Headers.Add("x-api-key", apiKeyHeader);
        patchReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var patchRes = await client.SendAsync(patchReq, cancellationToken);
        var patchBody = await patchRes.Content.ReadAsStringAsync(cancellationToken);

        if (!patchRes.IsSuccessStatusCode)
        {
            return BadRequest(new { error = $"Etsy ilan durumu '{targetState}' olarak güncellenemedi (HTTP {(int)patchRes.StatusCode}): {patchBody}" });
        }

        return Ok(new
        {
            success = true,
            listingId,
            state = targetState,
            message = $"İlan durumu başarıyla '{targetState}' olarak güncellendi."
        });
    }

    [HttpGet("listings/audits")]
    [EndpointSummary("Daha Önce Yapılan Listing AI Denetim Geçmişini Getir")]
    public async Task<IActionResult> GetListingAudits([FromQuery] string shopId = "53236321", CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var audits = await _settingsRepo.GetListingAuditsAsync(resolvedShopId, cancellationToken);
        return Ok(audits);
    }
}
