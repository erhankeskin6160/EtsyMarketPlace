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

        if (request.Price <= 0)
            return BadRequest(new { error = "Fiyat 0'dan büyük olmalıdır." });

        if (request.Quantity <= 0)
            return BadRequest(new { error = "Stok adedi en az 1 olmalıdır." });

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        if (string.IsNullOrWhiteSpace(keystring))
            return BadRequest(new { error = "Etsy Keystring (Client ID) bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin." });
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var client = _httpClientFactory.CreateClient();
        var postUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings";

        var formDict = new List<KeyValuePair<string, string>>
        {
            new("quantity", Math.Max(1, request.Quantity).ToString(CultureInfo.InvariantCulture)),
            new("title", request.Title.Trim()),
            new("description", string.IsNullOrWhiteSpace(request.Description) ? "Handmade artisan item." : request.Description.Trim()),
            new("price", request.Price.ToString("0.00", CultureInfo.InvariantCulture)),
            new("who_made", string.IsNullOrWhiteSpace(request.WhoMade) ? "i_did" : request.WhoMade.Trim()),
            new("when_made", string.IsNullOrWhiteSpace(request.WhenMade) ? "made_to_order" : request.WhenMade.Trim()),
            new("taxonomy_id", request.TaxonomyId > 0 ? request.TaxonomyId.ToString(CultureInfo.InvariantCulture) : "1042"),
            new("type", request.IsDigital ? "download" : "physical"),
            new("state", request.State?.ToLowerInvariant() == "active" ? "active" : "draft")
        };

        long? effectiveReadinessStateId = null;

        if (!request.IsDigital)
        {
            long? effectiveShippingProfileId = (request.ShippingProfileId.HasValue && request.ShippingProfileId.Value > 0)
                ? request.ShippingProfileId.Value
                : null;

            if (!effectiveShippingProfileId.HasValue)
            {
                try
                {
                    var profUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/shipping-profiles";
                    using var profReq = new HttpRequestMessage(HttpMethod.Get, profUrl);
                    profReq.Headers.Add("x-api-key", apiKeyHeader);
                    profReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                    using var profRes = await client.SendAsync(profReq, cancellationToken);
                    if (profRes.IsSuccessStatusCode)
                    {
                        var profBody = await profRes.Content.ReadAsStringAsync(cancellationToken);
                        using var profDoc = JsonDocument.Parse(profBody);
                        if (profDoc.RootElement.TryGetProperty("results", out var rArray) && rArray.GetArrayLength() > 0)
                        {
                            var first = rArray[0];
                            if (first.TryGetProperty("shipping_profile_id", out var spIdProp))
                            {
                                effectiveShippingProfileId = spIdProp.GetInt64();
                            }
                        }
                    }
                }
                catch { }
            }

            if (effectiveShippingProfileId.HasValue && effectiveShippingProfileId.Value > 0)
            {
                formDict.Add(new("shipping_profile_id", effectiveShippingProfileId.Value.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                return BadRequest(new { error = "Fiziksel ürünler için Etsy'de tanımlı bir Kargo Profili (shipping_profile_id) gereklidir. Lütfen Etsy Mağaza Yöneticisi > Settings > Shipping settings alanından bir kargo profili oluşturun veya ilanı 'Dijital' olarak seçin." });
            }

            effectiveReadinessStateId = (request.ReadinessStateId.HasValue && request.ReadinessStateId.Value > 0)
                ? request.ReadinessStateId.Value
                : null;

            if (!effectiveReadinessStateId.HasValue)
            {
                try
                {
                    var readyDefUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/readiness-state-definitions";
                    using var readyDefReq = new HttpRequestMessage(HttpMethod.Get, readyDefUrl);
                    readyDefReq.Headers.Add("x-api-key", apiKeyHeader);
                    readyDefReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                    using var readyDefRes = await client.SendAsync(readyDefReq, cancellationToken);
                    if (readyDefRes.IsSuccessStatusCode)
                    {
                        var readyDefBody = await readyDefRes.Content.ReadAsStringAsync(cancellationToken);
                        using var readyDefDoc = JsonDocument.Parse(readyDefBody);
                        if (readyDefDoc.RootElement.TryGetProperty("results", out var rdArray) && rdArray.GetArrayLength() > 0)
                        {
                            var firstRd = rdArray[0];
                            if (firstRd.TryGetProperty("readiness_state_id", out var rsIdProp))
                            {
                                effectiveReadinessStateId = rsIdProp.GetInt64();
                            }
                        }
                    }
                }
                catch { }
            }

            if (!effectiveReadinessStateId.HasValue)
            {
                try
                {
                    var activeListingsUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/active?limit=50";
                    using var actReq = new HttpRequestMessage(HttpMethod.Get, activeListingsUrl);
                    actReq.Headers.Add("x-api-key", apiKeyHeader);
                    actReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                    using var actRes = await client.SendAsync(actReq, cancellationToken);
                    if (actRes.IsSuccessStatusCode)
                    {
                        var actBody = await actRes.Content.ReadAsStringAsync(cancellationToken);
                        using var actDoc = JsonDocument.Parse(actBody);
                        if (actDoc.RootElement.TryGetProperty("results", out var aListings) && aListings.GetArrayLength() > 0)
                        {
                            foreach (var al in aListings.EnumerateArray())
                            {
                                if (al.TryGetProperty("readiness_state_id", out var rsProp) && rsProp.GetInt64() > 0)
                                {
                                    effectiveReadinessStateId = rsProp.GetInt64();
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (effectiveReadinessStateId.HasValue && effectiveReadinessStateId.Value > 0)
            {
                formDict.Add(new("readiness_state_id", effectiveReadinessStateId.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (request.Tags != null)
        {
            var validTags = request.Tags.Select(t => t.Trim()).Where(t => t.Length > 0 && t.Length <= 20).Take(13).ToList();
            foreach (var tag in validTags)
            {
                formDict.Add(new("tags[]", tag));
            }
        }

        if (request.Materials != null)
        {
            var validMats = request.Materials.Select(m => m.Trim()).Where(m => m.Length > 0).Take(13).ToList();
            foreach (var mat in validMats)
            {
                formDict.Add(new("materials[]", mat));
            }
        }

        using var postReq = new HttpRequestMessage(HttpMethod.Post, postUrl)
        {
            Content = new FormUrlEncodedContent(formDict)
        };
        postReq.Headers.Add("x-api-key", apiKeyHeader);
        postReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var postRes = await client.SendAsync(postReq, cancellationToken);
        var postBody = await postRes.Content.ReadAsStringAsync(cancellationToken);

        if (!postRes.IsSuccessStatusCode)
        {
            return BadRequest(new { error = $"Etsy ilan oluşturma başarısız (HTTP {(int)postRes.StatusCode}): {postBody}" });
        }

        long createdListingId = 0;
        string listingUrl = "";
        try
        {
            using var doc = JsonDocument.Parse(postBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("listing_id", out var lidProp))
            {
                createdListingId = lidProp.GetInt64();
            }
            else if (root.TryGetProperty("results", out var resProp) && resProp.GetArrayLength() > 0)
            {
                createdListingId = resProp[0].GetProperty("listing_id").GetInt64();
            }

            if (root.TryGetProperty("url", out var urlProp))
            {
                listingUrl = urlProp.GetString() ?? "";
            }
        }
        catch { }

        if (createdListingId <= 0)
        {
            return BadRequest(new { error = $"İlan oluşturuldu ancak listing_id okunamadı: {postBody}" });
        }

        if (string.IsNullOrWhiteSpace(listingUrl))
        {
            listingUrl = $"https://www.etsy.com/your/shops/me/listing-editor/edit/{createdListingId}";
        }

        int uploadedImageCount = 0;
        if (request.Images != null && request.Images.Count > 0)
        {
            int rank = 1;
            foreach (var img in request.Images.Take(10))
            {
                try
                {
                    byte[]? imageBytes = null;
                    string fileName = $"image_{rank}.jpg";

                    if (!string.IsNullOrWhiteSpace(img.DataUrl) && img.DataUrl.StartsWith("data:image/"))
                    {
                        var base64Part = img.DataUrl[(img.DataUrl.IndexOf(',') + 1)..];
                        imageBytes = Convert.FromBase64String(base64Part);
                    }
                    else if (!string.IsNullOrWhiteSpace(img.Url) && (img.Url.StartsWith("http://") || img.Url.StartsWith("https://")))
                    {
                        using var imgDownload = await client.GetAsync(img.Url, cancellationToken);
                        if (imgDownload.IsSuccessStatusCode)
                        {
                            imageBytes = await imgDownload.Content.ReadAsByteArrayAsync(cancellationToken);
                        }
                    }

                    if (imageBytes != null && imageBytes.Length > 0)
                    {
                        var imgUploadUrl = $"https://api.etsy.com/v3/application/shops/{resolvedShopId}/listings/{createdListingId}/images";
                        using var imgContent = new MultipartFormDataContent();
                        var fileContent = new ByteArrayContent(imageBytes);
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                        imgContent.Add(fileContent, "image", fileName);
                        imgContent.Add(new StringContent(rank.ToString(CultureInfo.InvariantCulture)), "rank");

                        using var imgReq = new HttpRequestMessage(HttpMethod.Post, imgUploadUrl)
                        {
                            Content = imgContent
                        };
                        imgReq.Headers.Add("x-api-key", apiKeyHeader);
                        imgReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                        using var imgRes = await client.SendAsync(imgReq, cancellationToken);
                        if (imgRes.IsSuccessStatusCode)
                        {
                            uploadedImageCount++;
                        }
                    }
                }
                catch { }
                rank++;
            }
        }

        int uploadedVariationCount = 0;
        if (request.Variations != null && request.Variations.Count > 0)
        {
            try
            {
                var groups = new List<(string Name, long PropertyId, List<string> Values)>();

                if (request.VariationGroups != null && request.VariationGroups.Count > 0)
                {
                    int gIdx = 1;
                    foreach (var g in request.VariationGroups.Take(2))
                    {
                        var (name, propId) = FastListingDraftHelper.ParseVariationType(g.Name, gIdx);
                        var vals = g.Values.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        if (vals.Count > 0)
                        {
                            groups.Add((name, propId, vals));
                            gIdx++;
                        }
                    }
                }

                if (groups.Count == 0)
                {
                    var distinctKeys = request.Variations.Select(v => v.Key).Distinct().ToList();
                    bool hasCompoundKeys = distinctKeys.Any(k => k.Contains('/'));
                    if (hasCompoundKeys)
                    {
                        var g1Vals = distinctKeys.Select(k => k.Split('/')[0].Trim()).Where(v => v.Length > 0).Distinct().ToList();
                        var g2Vals = distinctKeys.Where(k => k.Contains('/')).Select(k => k.Split('/')[1].Trim()).Where(v => v.Length > 0).Distinct().ToList();
                        groups.Add(("Size", 513, g1Vals));
                        if (g2Vals.Count > 0)
                        {
                            groups.Add(("Color", 514, g2Vals));
                        }
                    }
                    else
                    {
                        var g1Vals = distinctKeys.Select(k => k.Trim()).Where(v => v.Length > 0).Distinct().ToList();
                        groups.Add(("Custom", 513, g1Vals));
                    }
                }

                var customPricingMap = request.Variations.ToDictionary(v => v.Key.Trim(), v => v, StringComparer.OrdinalIgnoreCase);
                var productsList = new List<object>();
                var distinctPricesG1 = new HashSet<decimal>();
                var distinctPricesG2 = new HashSet<decimal>();
                var sharedSku = $"AUTO-{createdListingId}";

                var combinations = new List<List<(string GroupName, long PropId, string Value)>>();
                if (groups.Count == 1)
                {
                    foreach (var val in groups[0].Values)
                    {
                        combinations.Add(new() { (groups[0].Name, groups[0].PropertyId, val) });
                    }
                }
                else if (groups.Count >= 2)
                {
                    foreach (var val1 in groups[0].Values)
                    {
                        foreach (var val2 in groups[1].Values)
                        {
                            combinations.Add(new()
                            {
                                (groups[0].Name, groups[0].PropertyId, val1),
                                (groups[1].Name, groups[1].PropertyId, val2)
                            });
                        }
                    }
                }

                int prodIdx = 1;
                foreach (var combo in combinations.Take(70))
                {
                    var comboKey = string.Join(" / ", combo.Select(c => c.Value));
                    decimal rowPrice = request.Price;
                    int rowQty = Math.Max(1, request.Quantity);
                    bool isEnabled = true;

                    if (customPricingMap.TryGetValue(comboKey, out var matchedVar) ||
                        (combo.Count > 0 && customPricingMap.TryGetValue(combo[0].Value, out matchedVar)))
                    {
                        rowPrice = matchedVar.Price > 0 ? matchedVar.Price : request.Price;
                        rowQty = matchedVar.Quantity > 0 ? matchedVar.Quantity : Math.Max(1, request.Quantity);
                        isEnabled = matchedVar.Active;
                    }

                    distinctPricesG1.Add(rowPrice);
                    if (combo.Count > 1) distinctPricesG2.Add(rowPrice);

                    var offering = new Dictionary<string, object>
                    {
                        ["price"] = rowPrice.ToString("0.00", CultureInfo.InvariantCulture),
                        ["quantity"] = Math.Max(1, rowQty),
                        ["is_enabled"] = isEnabled
                    };
                    if (effectiveReadinessStateId.HasValue && effectiveReadinessStateId.Value > 0)
                    {
                        offering["readiness_state_id"] = effectiveReadinessStateId.Value;
                    }

                    productsList.Add(new
                    {
                        sku = $"{sharedSku}-{prodIdx}",
                        property_values = combo.Select(c => new
                        {
                            property_id = c.PropId,
                            property_name = c.GroupName,
                            values = new[] { c.Value }
                        }).ToList(),
                        offerings = new[] { offering }
                    });
                    prodIdx++;
                }

                var priceOnProperty = new List<long>();
                if (distinctPricesG1.Count > 1 && groups.Count >= 1)
                {
                    priceOnProperty.Add(groups[0].PropertyId);
                }
                if (distinctPricesG2.Count > 1 && groups.Count >= 2 && !priceOnProperty.Contains(groups[1].PropertyId))
                {
                    priceOnProperty.Add(groups[1].PropertyId);
                }

                var invPayload = new
                {
                    products = productsList,
                    price_on_property = priceOnProperty,
                    quantity_on_property = Array.Empty<long>(),
                    sku_on_property = Array.Empty<long>()
                };

                var invUrl = $"https://api.etsy.com/v3/application/listings/{createdListingId}/inventory";
                using var invReq = new HttpRequestMessage(HttpMethod.Put, invUrl)
                {
                    Content = new StringContent(JsonSerializer.Serialize(invPayload), Encoding.UTF8, "application/json")
                };
                invReq.Headers.Add("x-api-key", apiKeyHeader);
                invReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                using var invRes = await client.SendAsync(invReq, cancellationToken);
                if (invRes.IsSuccessStatusCode)
                {
                    uploadedVariationCount = productsList.Count;
                }
            }
            catch { }
        }

        return Ok(new
        {
            success = true,
            listingId = createdListingId,
            url = listingUrl,
            state = request.State?.ToLowerInvariant() == "active" ? "active" : "draft",
            uploadedImages = uploadedImageCount,
            variationsCount = uploadedVariationCount,
            message = $"İlan başarıyla Etsy'ye aktarıldı! (Listing ID: {createdListingId})"
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
