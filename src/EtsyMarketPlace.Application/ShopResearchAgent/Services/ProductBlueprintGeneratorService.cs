namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Domain.ShopResearchAgent;

/// <summary>
/// Generates differentiated Etsy Product Blueprints (55-char mobile title, 140-char SEO title,
/// 13 long-tail tags <=20 chars, 3-tier Variation Ladder, competitor gap, and AI mockup brief).
/// </summary>
public sealed class ProductBlueprintGeneratorService
{
    public const string UsageModuleName = "Mağaza Araştırma Ajanı";

    private static readonly string[] FillerWords =
    [
        "free shipping", "best seller", "bestseller", "fast shipping", "ready to ship", "on sale"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IGeminiListingApiClient? _llmClient;
    private readonly IpRiskPolicyService _ipRiskPolicy;
    private readonly IAiUsageRecorder? _usageRecorder;

    public ProductBlueprintGeneratorService(
        IGeminiListingApiClient? llmClient = null,
        IpRiskPolicyService? ipRiskPolicy = null,
        IAiUsageRecorder? usageRecorder = null)
    {
        _llmClient = llmClient;
        _ipRiskPolicy = ipRiskPolicy ?? new IpRiskPolicyService();
        _usageRecorder = usageRecorder;
    }

    public ProductBlueprintDto BuildDeterministicBlueprint(
        string title,
        decimal basePriceUsd,
        IReadOnlyList<string>? rawTags,
        IReadOnlyList<string>? shopTopTags,
        IReadOnlyList<MarketTagFrequencyDto>? marketTopTags,
        IReadOnlyList<string>? preferredMaterials,
        bool supports3DPrint,
        string category,
        decimal topShopConcentrationPct,
        decimal freeShippingRatioPct,
        IReadOnlyList<string>? forbiddenKeywords = null)
    {
        var safeBasePrice = basePriceUsd > 0m ? Math.Round(basePriceUsd, 2) : 29.90m;
        var cleanTitle = SanitizeTitle(title, forbiddenKeywords);
        var mobileTitle = BuildMobileTitle55(cleanTitle, category);
        var materials = ResolveMaterials(preferredMaterials, supports3DPrint);
        var fullTitle = BuildFullTitle140(mobileTitle, cleanTitle, materials, supports3DPrint);
        var longTailTags = Build13LongTailTags(rawTags, shopTopTags, marketTopTags, forbiddenKeywords);
        var ladder = BuildVariationLadder(safeBasePrice);

        var diffAngle = supports3DPrint
            ? $"Mat/saten {string.Join(" & ", materials.Take(2))} doku + kişiselleştirilebilir isim/renk plakası ve 3 kademeli fiyat merdiveni ile ham rakiplerden ayrışın."
            : $"{string.Join(" & ", materials.Take(2))} malzeme kalitesi, hediye kutusu sunumu ve çoklu paket seçeneğiyle algılanan değeri artırın.";

        var compGap = topShopConcentrationPct >= 45m
            ? $"Nişte üst mağaza yoğunlaşması yüksek (%{topShopConcentrationPct:F0}). İlk 55 krk mobil başlık + niş uzun kuyruk etiketlerle alt segmente odaklanın."
            : freeShippingRatioPct >= 65m
                ? $"Rakiplerin %{freeShippingRatioPct:F0}'i ücretsiz kargo sunuyor; kargoyu fiyata gömerek 2'li setle sepet ortalamasını yükseltin."
                : $"Tekelleşme düşük (%{topShopConcentrationPct:F0}); temiz stüdyo yaşam alanı mockup'ı ve varyasyon seçeneğiyle ilk sayfada hızlı yer açılabilir.";

        var mockupBrief = supports3DPrint
            ? $"Sıcak İskandinav ahşap masa üstünde mat dokulu 3D baskı ürün, yumuşak yan gün ışığı, yanında defter/kahve kupası ile ölçek referansı (temiz EXIF, filigransız)."
            : $"Minimalist doğal taş/ahşap arka plan üzerinde stüdyo çekimi, 3/4 açı ana görsel + yakın doku detayı + hediye kutulu yan görsel.";

        return new ProductBlueprintDto(
            mobileTitle,
            fullTitle,
            longTailTags,
            ladder,
            diffAngle,
            compGap,
            mockupBrief,
            materials);
    }

    public async Task<(int PromptTokens, int CompletionTokens, int TotalTokens, decimal EstimatedCostUsd)> EnrichTopCandidatesAsync(
        IReadOnlyList<ResearchCandidate> winners,
        ShopDnaProfile dna,
        IReadOnlyDictionary<Guid, IReadOnlyList<MarketTagFrequencyDto>>? marketTagsByRunOrCandidate = null,
        string? model = null,
        string? apiKey = null,
        bool useAi = false,
        CancellationToken ct = default)
    {
        if (winners == null || winners.Count == 0)
            return (0, 0, 0, 0m);

        var blueprintsById = new Dictionary<Guid, ProductBlueprintDto>();
        foreach (var c in winners)
        {
            IReadOnlyList<MarketTagFrequencyDto>? mTags = null;
            if (marketTagsByRunOrCandidate != null && marketTagsByRunOrCandidate.TryGetValue(c.CandidateId, out var foundTags))
                mTags = foundTags;

            var bp = BuildDeterministicBlueprint(
                c.Title,
                c.EstimatedSellPriceUsd,
                c.SuggestedTags,
                dna.TopPerformingTags,
                mTags,
                dna.PreferredMaterials,
                dna.Supports3DPrint,
                c.Category,
                c.TopShopConcentrationPct,
                c.FreeShippingRatioPct,
                dna.ForbiddenKeywords);

            blueprintsById[c.CandidateId] = bp;
        }

        var promptTokens = 0;
        var completionTokens = 0;
        var totalTokens = 0;
        var costUsd = 0m;

        // Single batched LLM call ONLY for top 5 winners when user opted into Balanced/FullAi mode
        if (useAi && _llmClient != null && !string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var topBatch = winners.Take(5).Select(c => new
                {
                    candidateId = c.CandidateId,
                    rawTitle = c.Title,
                    category = c.Category,
                    sellPriceUsd = c.EstimatedSellPriceUsd,
                    monopolyPct = c.TopShopConcentrationPct,
                    freeShippingPct = c.FreeShippingRatioPct,
                    tags = blueprintsById[c.CandidateId].LongTailTags.Take(6)
                }).ToList();

                var sysPrompt =
                    "Sen kıdemli bir Etsy SEO ve Ürün Farklılaştırma uzmanısın. Verilen ürün adayları için rakipten kopyalamayan, " +
                    "telif/marka kelimesi içermeyen, yüksek dönüşümlü ürün blueprint'i üret. " +
                    "KISITLAR: mobileTitle55 en fazla 55 karakter (İngilizce), fullTitle140 en fazla 140 karakter (İngilizce), " +
                    "differentiationAngle/competitorGap/aiMockupBrief kısa ve öz Türkçe. " +
                    "Yanıtı YALNIZCA geçerli bir JSON nesnesi olarak ver: " +
                    "{\"blueprints\":[{\"candidateId\":\"guid\",\"mobileTitle55\":\"...\",\"fullTitle140\":\"...\",\"differentiationAngle\":\"...\",\"competitorGap\":\"...\",\"aiMockupBrief\":\"...\"}]}";

                var userPrompt = JsonSerializer.Serialize(new
                {
                    shopNiche = dna.PrimaryNiche,
                    supports3DPrint = dna.Supports3DPrint,
                    materials = dna.PreferredMaterials.Take(4),
                    candidates = topBatch
                }, JsonOptions);

                var resolvedModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.5-flash" : model;
                var llmResult = await _llmClient.GenerateOptimizationJsonWithUsageAsync(
                    sysPrompt, userPrompt, resolvedModel, apiKey, maxOutputTokens: 1200, ct);

                promptTokens = llmResult.PromptTokens;
                completionTokens = llmResult.CompletionTokens;
                totalTokens = llmResult.TotalTokens > 0 ? llmResult.TotalTokens : promptTokens + completionTokens;
                costUsd = AiPriceCalculator.CalculateCostUsd(
                    string.IsNullOrWhiteSpace(llmResult.ResolvedModel) ? resolvedModel : llmResult.ResolvedModel,
                    promptTokens,
                    completionTokens);

                if (_usageRecorder != null)
                {
                    await _usageRecorder.RecordAsync(
                        UsageModuleName,
                        llmResult,
                        $"Ürün Blueprint (Top {topBatch.Count})",
                        ct);
                }

                if (llmResult.Success && !string.IsNullOrWhiteSpace(llmResult.Content))
                {
                    MergeLlmBlueprints(llmResult.Content, blueprintsById, dna.ForbiddenKeywords);
                }
            }
            catch
            {
                // Keep deterministic blueprints safely on any LLM error
            }
        }

        foreach (var c in winners)
        {
            var bp = blueprintsById[c.CandidateId];
            c.MobileTitle55 = bp.MobileTitle55;
            c.SuggestedTitle = bp.FullTitle140;
            c.SuggestedTags = bp.LongTailTags;
            c.BlueprintJson = JsonSerializer.Serialize(bp, JsonOptions);
        }

        return (promptTokens, completionTokens, totalTokens, costUsd);
    }

    private void MergeLlmBlueprints(
        string rawJson,
        Dictionary<Guid, ProductBlueprintDto> map,
        IReadOnlyList<string> forbiddenKeywords)
    {
        var cleaned = StripFences(rawJson);
        using var doc = JsonDocument.Parse(cleaned);
        if (!doc.RootElement.TryGetProperty("blueprints", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var el in arr.EnumerateArray())
        {
            if (!el.TryGetProperty("candidateId", out var idEl) || !Guid.TryParse(idEl.GetString(), out var cid))
                continue;
            if (!map.TryGetValue(cid, out var existing))
                continue;

            var llmMobile = el.TryGetProperty("mobileTitle55", out var mEl) ? mEl.GetString() : null;
            var llmFull = el.TryGetProperty("fullTitle140", out var fEl) ? fEl.GetString() : null;
            var llmDiff = el.TryGetProperty("differentiationAngle", out var dEl) ? dEl.GetString() : null;
            var llmGap = el.TryGetProperty("competitorGap", out var gEl) ? gEl.GetString() : null;
            var llmMock = el.TryGetProperty("aiMockupBrief", out var mkEl) ? mkEl.GetString() : null;

            var safeMobile = IsSafeTitle(llmMobile, forbiddenKeywords)
                ? TrimOnWordBoundary(llmMobile!.Trim(), 55)
                : existing.MobileTitle55;
            var safeFull = IsSafeTitle(llmFull, forbiddenKeywords)
                ? TrimOnWordBoundary(llmFull!.Trim(), 140)
                : existing.FullTitle140;

            map[cid] = existing with
            {
                MobileTitle55 = safeMobile,
                FullTitle140 = safeFull,
                DifferentiationAngle = !string.IsNullOrWhiteSpace(llmDiff) ? llmDiff.Trim() : existing.DifferentiationAngle,
                CompetitorGap = !string.IsNullOrWhiteSpace(llmGap) ? llmGap.Trim() : existing.CompetitorGap,
                AiMockupBrief = !string.IsNullOrWhiteSpace(llmMock) ? llmMock.Trim() : existing.AiMockupBrief
            };
        }
    }

    private bool IsSafeTitle(string? candidate, IReadOnlyList<string> forbiddenKeywords)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Trim().Length < 8)
            return false;

        var eval = _ipRiskPolicy.Evaluate(candidate, null, null, forbiddenKeywords);
        return eval.Decision != IpRiskDecision.Block;
    }

    private static string SanitizeTitle(string title, IReadOnlyList<string>? forbiddenKeywords)
    {
        var result = (title ?? string.Empty).Trim();
        foreach (var filler in FillerWords)
        {
            result = Regex.Replace(result, $@"\b{Regex.Escape(filler)}\b", string.Empty, RegexOptions.IgnoreCase);
        }

        if (forbiddenKeywords != null)
        {
            foreach (var kw in forbiddenKeywords.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                result = Regex.Replace(result, $@"\b{Regex.Escape(kw.Trim())}\b", string.Empty, RegexOptions.IgnoreCase);
            }
        }

        result = Regex.Replace(result, @"\s{2,}", " ").Trim(' ', '-', '|', ',', '/');
        return string.IsNullOrWhiteSpace(result) ? "Minimalist Custom Home Decor Gift" : result;
    }

    private static string BuildMobileTitle55(string cleanTitle, string category)
    {
        var candidate = cleanTitle;
        if (candidate.Length < 18 && !string.IsNullOrWhiteSpace(category))
            candidate = $"{candidate} - {category}";

        return TrimOnWordBoundary(candidate, 55);
    }

    private static string BuildFullTitle140(
        string mobileTitle,
        string cleanTitle,
        IReadOnlyList<string> materials,
        bool supports3DPrint)
    {
        var basePart = cleanTitle.Length > mobileTitle.Length ? cleanTitle : mobileTitle;
        var suffix = supports3DPrint
            ? $"Custom {materials.FirstOrDefault() ?? "PLA"} 3D Printed Decor, Modern Desk & Housewarming Gift"
            : $"Handcrafted {materials.FirstOrDefault() ?? "Artisan"} Accent, Unique Personalized Gift Idea";

        var combined = basePart.Contains("gift", StringComparison.OrdinalIgnoreCase)
            ? $"{basePart} | {materials.FirstOrDefault() ?? "Studio"} Edition"
            : $"{basePart} | {suffix}";

        return TrimOnWordBoundary(combined, 140);
    }

    private static IReadOnlyList<string> Build13LongTailTags(
        IReadOnlyList<string>? rawTags,
        IReadOnlyList<string>? shopTopTags,
        IReadOnlyList<MarketTagFrequencyDto>? marketTopTags,
        IReadOnlyList<string>? forbiddenKeywords)
    {
        var orderedMarket = (marketTopTags ?? Array.Empty<MarketTagFrequencyDto>())
            .OrderByDescending(t => t.IsLongTail)
            .ThenBy(t => string.Equals(t.CompetitionLevel, "Yüksek", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(t => t.Count)
            .Select(t => t.Tag);

        var forbidden = new HashSet<string>(
            (forbiddenKeywords ?? Array.Empty<string>()).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var result = new List<string>();
        foreach (var raw in orderedMarket.Concat(rawTags ?? Array.Empty<string>()).Concat(shopTopTags ?? Array.Empty<string>()))
        {
            var clean = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (clean.Length < 2) continue;
            if (clean.Length > 20) clean = TrimOnWordBoundary(clean, 20).ToLowerInvariant();
            if (clean.Length < 2) continue;
            if (forbidden.Any(f => clean.Contains(f, StringComparison.OrdinalIgnoreCase))) continue;

            if (!result.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(clean);
            }
            if (result.Count >= 13) break;
        }

        return result;
    }

    private static IReadOnlyList<VariationTierDto> BuildVariationLadder(decimal basePriceUsd) =>
    [
        new VariationTierDto(
            "Giriş",
            "Mini / Tekli",
            Math.Max(9.99m, Math.Round(basePriceUsd * 0.75m, 2)),
            "Düşük giriş fiyatı ile hızlı ilk satış ve mağaza favorisi toplama"),
        new VariationTierDto(
            "Ana",
            "Standart + Renk Seçimi",
            Math.Round(basePriceUsd, 2),
            "Ana kâr motoru; mağaza DNA'sına uygun ölçü & renk varyasyonu"),
        new VariationTierDto(
            "Premium Set",
            "Kişiselleştirilebilir Set",
            Math.Round(basePriceUsd * 1.38m, 2),
            "İsim/not kişiselleştirmesi veya 2'li paket ile yüksek sepet ortalaması")
    ];

    private static IReadOnlyList<string> ResolveMaterials(IReadOnlyList<string>? preferredMaterials, bool supports3DPrint)
    {
        var list = (preferredMaterials ?? Array.Empty<string>())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        if (list.Count > 0) return list;
        return supports3DPrint ? ["PLA", "Matte Filament"] : ["Wood", "Ceramic"];
    }

    private static string TrimOnWordBoundary(string input, int maxLen)
    {
        var clean = (input ?? string.Empty).Trim();
        if (clean.Length <= maxLen) return clean;
        var slice = clean[..maxLen];
        var lastSpace = slice.LastIndexOf(' ');
        return (lastSpace >= maxLen / 2 ? slice[..lastSpace] : slice).TrimEnd(' ', '-', '|', ',');
    }

    private static string StripFences(string raw)
    {
        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean[7..];
        else if (clean.StartsWith("```", StringComparison.Ordinal)) clean = clean[3..];
        if (clean.EndsWith("```", StringComparison.Ordinal)) clean = clean[..^3];
        return clean.Trim();
    }
}
