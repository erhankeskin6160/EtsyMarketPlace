using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Application.Shipping;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Listings.Commands.RunVariationPricingAgent;

/// <summary>
/// Hızlı Ürün Ekle ekranı için otonom AI Fiyat, Rakip Analizi ve Kargo Maliyeti Hesaplama Ajanı.
/// </summary>
public sealed class RunVariationPricingAgentHandler(
    IMarketResearchService marketResearchService,
    IShipmentOrchestratorService shipmentOrchestrator,
    IGeminiListingApiClient aiClient)
    : IRequestHandler<RunVariationPricingAgentCommand, ServiceResponse>
{
    private const string SingleProductSeedKey = "__SINGLE_PRODUCT__";

    private static readonly Regex DimensionRegex = new(
        @"(\d+(?:[.,]\d+)?)\s*(cm|mm|in(?:ch|ches)?|"")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PackCountRegex = new(
        @"(?:(\d+)\s*(?:pcs|pieces|pack|adet|set|'lı|'li|'lü)|(?:set\s+of|pack\s+of)\s+(\d+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "for", "and", "with", "the", "a", "an", "of", "to", "in", "on", "gift", "ideas",
        "handmade", "custom", "personalized", "unique", "best", "kids", "adults"
    };

    public async Task<ServiceResponse> Handle(RunVariationPricingAgentCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var dto = request.Request;
        if (dto is null || string.IsNullOrWhiteSpace(dto.Title))
            return ServiceResponse.CreateError("Fiyatlandırma ajanı için en az bir ürün başlığı gereklidir.", HttpStatusCode.BadRequest);

        var shopId = string.IsNullOrWhiteSpace(dto.ShopId) ? "53236321" : dto.ShopId.Trim();
        var usdTryRate = dto.UsdTryRate > 0m ? dto.UsdTryRate : 50.45m;
        var baseWeightGrams = Math.Clamp(dto.BaseWeightGrams > 0 ? dto.BaseWeightGrams : 250, 50, 25_000);
        var targetCountry = string.IsNullOrWhiteSpace(dto.TargetCountryCode) ? "US" : dto.TargetCountryCode.Trim().ToUpperInvariant();
        var targetMarginPct = Math.Clamp(dto.TargetNetMarginPercent, 5m, 65m);
        var productionCost = Math.Max(0m, dto.ProductionCostUsd);
        var candidateKeywords = BuildCandidateSearchQueries(dto.Title, dto.Tags, dto.Category);

        var steps = new List<VariationAgentStepDto>();

        // 1. Adım: Canlı Etsy Rakip Taraması (çok aşamalı sorgu fallback + aykırı değer temizliği)
        var (marketBenchmark, searchKeyword) = await FetchMarketBenchmarkAsync(candidateKeywords, shopId, cancellationToken);
        var outlierNote = marketBenchmark.FilteredOutlierCount > 0
            ? $", {marketBenchmark.FilteredOutlierCount} aşırı düşük aykırı ilan elendi"
            : string.Empty;
        steps.Add(new VariationAgentStepDto
        {
            StepNumber = 1,
            ToolName = "MarketResearchTool",
            Title = "Etsy Rakip Analizi",
            Status = marketBenchmark.CompetitorCount > 0 ? "completed" : "fallback",
            Summary = marketBenchmark.CompetitorCount > 0
                ? $"'{searchKeyword}' için {marketBenchmark.CompetitorCount} rakip ilan tarandı. Min ${marketBenchmark.MinPriceUsd:F2} · Medyan ${marketBenchmark.MedianPriceUsd:F2} · P75 ${marketBenchmark.P75PriceUsd:F2} (Ücretsiz Kargo: %{marketBenchmark.FreeShippingRatioPct:F0}{outlierNote})."
                : $"Canlı Etsy aramasından '{searchKeyword}' için eşleşme dönmedi (mağaza yetkisi veya niş filtre); maliyet + hedef kâr marjı baz alındı."
        });

        // 2. Adım: Varyasyon Kombinasyonları ve Ağırlık/Desi Profili
        var variationPlan = BuildVariationPlan(dto, baseWeightGrams);

        // 3. Adım: GTİP Seçimi & 4 Taşıyıcılı Kargo Modülü Teklifleri
        var hsCode = ResolveHsCode(dto.Title, dto.Category);
        var (shippingBenchmark, weightBracketPrices) = await FetchShippingQuotesAsync(
            dto, targetCountry, hsCode, usdTryRate, baseWeightGrams, variationPlan.Seeds, cancellationToken);

        steps.Add(new VariationAgentStepDto
        {
            StepNumber = 2,
            ToolName = "ShippingModuleTool",
            Title = "Kargo Modülü & GTİP Hesabı",
            Status = "completed",
            Summary = $"{shippingBenchmark.SelectedCarrierName} ({shippingBenchmark.SelectedServiceName}) seçildi: ${shippingBenchmark.BaseShippingCostUsd:F2} (₺{shippingBenchmark.BaseShippingCostTry:F0}) · {shippingBenchmark.CalculatedDesi:F2} desi · GTİP {hsCode}."
        });

        // 4. Adım: Etsy Türkiye Komisyon & Sıfır Zarar Başabaş Kalkanı
        var preparedRows = PrepareVariationBreakEvenModels(
            variationPlan.Seeds,
            weightBracketPrices,
            shippingBenchmark.BaseShippingCostUsd,
            productionCost,
            targetMarginPct,
            dto.IncludeOffsiteAds,
            usdTryRate,
            marketBenchmark);

        var minFloor = preparedRows.Min(r => r.FloorPriceUsd);
        var maxFloor = preparedRows.Max(r => r.FloorPriceUsd);
        steps.Add(new VariationAgentStepDto
        {
            StepNumber = 3,
            ToolName = "EtsyFeeGuardrail",
            Title = "Etsy Kesinti & Zarar Kalkanı",
            Status = "completed",
            Summary = $"Türkiye kesintileri (%16.92 + $0.31{(dto.IncludeOffsiteAds ? " + %15 Offsite Ads" : "")}) ve kargo düşülerek taban koruma bandı ${minFloor:F2}–${maxFloor:F2} olarak kilitlendi."
        });

        // 5. Adım: LLM Stratejik Fiyatlandırma (anahtar yoksa veya hata olursa deterministik)
        var (usedAi, providerLabel, strategySummary, llmOverrides) = await TryRunLlmPricingAsync(
            dto, searchKeyword, marketBenchmark, shippingBenchmark, preparedRows, variationPlan.IsSingleProduct, cancellationToken);

        var pricedRows = FinalizePricedVariations(
            preparedRows,
            llmOverrides,
            dto.IncludeOffsiteAds,
            usdTryRate);

        decimal basePrice;
        decimal avgMargin;
        var baseShipping = shippingBenchmark.BaseShippingCostUsd;
        PricedVariationRowDto? anchorRow;

        if (variationPlan.IsSingleProduct)
        {
            anchorRow = pricedRows.FirstOrDefault();
            basePrice = anchorRow?.RecommendedPriceUsd ?? 19.90m;
            avgMargin = anchorRow?.NetMarginPercent ?? targetMarginPct;

            steps.Add(new VariationAgentStepDto
            {
                StepNumber = 4,
                ToolName = "LlmPricingStrategist",
                Title = "LLM Tekil Ürün Fiyatlandırması",
                Status = "completed",
                Summary = $"Tekil ürün (varyasyonsuz) {providerLabel} ile fiyatlandı: önerilen ${basePrice:F2}, net kâr ${(anchorRow?.NetProfitUsd ?? 0m):F2} (%{avgMargin:F1} net marj)."
            });
        }
        else
        {
            anchorRow = pricedRows.OrderBy(r => r.RecommendedPriceUsd).FirstOrDefault();
            basePrice = anchorRow?.RecommendedPriceUsd ?? 19.90m;
            avgMargin = pricedRows.Count > 0
                ? Math.Round(pricedRows.Average(r => r.NetMarginPercent), 1)
                : targetMarginPct;

            steps.Add(new VariationAgentStepDto
            {
                StepNumber = 4,
                ToolName = "LlmPricingStrategist",
                Title = "LLM Varyasyon Fiyatlandırması",
                Status = "completed",
                Summary = $"{pricedRows.Count} varyasyon {providerLabel} ile fiyatlandı: taban ${basePrice:F2}, ort. net marj %{avgMargin:F1}."
            });
        }

        var result = new VariationPricingAgentResultDto
        {
            IsSingleProductPricing = variationPlan.IsSingleProduct,
            UsedAi = usedAi,
            ProviderUsed = providerLabel,
            SearchKeywordUsed = searchKeyword,
            MarketBenchmark = marketBenchmark,
            ShippingBenchmark = shippingBenchmark,
            RecommendedBasePriceUsd = basePrice,
            RecommendedBaseShippingCostUsd = baseShipping,
            AverageNetMarginPercent = avgMargin,
            RecommendedBreakEvenPriceUsd = anchorRow?.BreakEvenPriceUsd ?? 0m,
            RecommendedEtsyFeesUsd = anchorRow?.EtsyFeesUsd ?? 0m,
            RecommendedNetProfitUsd = anchorRow?.NetProfitUsd ?? 0m,
            RecommendedPricingTierLabel = anchorRow?.PricingTierLabel ?? string.Empty,
            RecommendedPricingReasoning = anchorRow?.Reasoning ?? string.Empty,
            StrategySummary = strategySummary,
            SuggestedVarType1 = variationPlan.IsSingleProduct ? string.Empty : variationPlan.VarType1,
            SuggestedVarValues1 = variationPlan.IsSingleProduct ? string.Empty : variationPlan.VarValues1,
            SuggestedEnableVar2 = !variationPlan.IsSingleProduct && variationPlan.EnableVar2,
            SuggestedVarType2 = variationPlan.IsSingleProduct ? string.Empty : variationPlan.VarType2,
            SuggestedVarValues2 = variationPlan.IsSingleProduct ? string.Empty : variationPlan.VarValues2,
            AgentSteps = steps,
            PricedVariations = variationPlan.IsSingleProduct ? [] : pricedRows
        };

        return ServiceResponse.CreateSuccess("AI Fiyat, Rakip ve Kargo Ajanı analizini tamamladı.", result);
    }

    private async Task<(VariationMarketBenchmarkDto Benchmark, string ResolvedKeyword)> FetchMarketBenchmarkAsync(
        IReadOnlyList<string> candidateKeywords,
        string shopId,
        CancellationToken cancellationToken)
    {
        var primaryKeyword = candidateKeywords.Count > 0 ? candidateKeywords[0] : "handmade gift";
        List<MarketListingItemDto> bestListings = [];
        var resolvedKeyword = primaryKeyword;

        foreach (var keyword in candidateKeywords)
        {
            try
            {
                var market = await marketResearchService.SearchMarketAsync(
                    new MarketSearchQuery(keyword, Limit: 25, SortBy: "market_score", ShopId: shopId),
                    cancellationToken);

                var priced = (market.Listings ?? [])
                    .Where(l => l.PriceUsd > 0m)
                    .ToList();

                if (priced.Count > bestListings.Count)
                {
                    bestListings = priced;
                    resolvedKeyword = keyword;
                }

                if (priced.Count >= 3)
                    break;
            }
            catch
            {
                // Try next fallback keyword if Etsy search fails for this query
            }
        }

        if (bestListings.Count == 0)
        {
            return (new VariationMarketBenchmarkDto { UsedLiveMarketData = false }, primaryKeyword);
        }

        var (filteredListings, filteredOutlierCount) = FilterExtremeLowOutliers(bestListings);
        var sortedPrices = filteredListings.Select(l => l.PriceUsd).OrderBy(p => p).ToList();
        var median = ComputeMedian(sortedPrices);
        var p75 = sortedPrices[Math.Min(sortedPrices.Count - 1, (int)Math.Floor(sortedPrices.Count * 0.75))];
        var avg = Math.Round(sortedPrices.Average(), 2);

        var freeShippingPrices = filteredListings.Where(l => l.HasFreeShipping).Select(l => l.PriceUsd).OrderBy(p => p).ToList();
        var paidShippingPrices = filteredListings.Where(l => !l.HasFreeShipping).Select(l => l.PriceUsd).OrderBy(p => p).ToList();

        var topSamples = filteredListings
            .OrderByDescending(l => l.IsBestseller)
            .ThenByDescending(l => l.EstimatedMonthlySales)
            .ThenByDescending(l => l.MarketScore)
            .Take(5)
            .Select(l => new CompetitorPriceSampleDto
            {
                ListingId = l.Id,
                Title = l.Title,
                PriceUsd = l.PriceUsd,
                ShopName = l.ShopName,
                ShopSales = l.ShopSales,
                HasFreeShipping = l.HasFreeShipping,
                EstimatedMonthlySales = l.EstimatedMonthlySales
            })
            .ToList();

        var benchmark = new VariationMarketBenchmarkDto
        {
            CompetitorCount = filteredListings.Count,
            MinPriceUsd = sortedPrices.First(),
            MedianPriceUsd = median,
            AveragePriceUsd = avg,
            P75PriceUsd = p75,
            MaxPriceUsd = sortedPrices.Last(),
            FreeShippingMedianUsd = ComputeMedian(freeShippingPrices),
            PaidShippingMedianUsd = ComputeMedian(paidShippingPrices),
            FilteredOutlierCount = filteredOutlierCount,
            FreeShippingRatioPct = Math.Round((double)freeShippingPrices.Count / filteredListings.Count * 100.0, 1),
            UsedLiveMarketData = true,
            TopCompetitors = topSamples
        };

        return (benchmark, resolvedKeyword);
    }

    private static (List<MarketListingItemDto> Filtered, int RemovedCount) FilterExtremeLowOutliers(
        List<MarketListingItemDto> listings)
    {
        if (listings.Count < 5)
            return (listings, 0);

        var rawPrices = listings.Select(l => l.PriceUsd).OrderBy(p => p).ToList();
        var rawMedian = ComputeMedian(rawPrices);
        var lowerCutoff = Math.Round(rawMedian * 0.30m, 2);

        var filtered = listings.Where(l => l.PriceUsd >= lowerCutoff).ToList();
        if (filtered.Count < 3 || filtered.Count == listings.Count)
            return (listings, 0);

        return (filtered, listings.Count - filtered.Count);
    }

    private static decimal ComputeMedian(IReadOnlyList<decimal> sortedPrices)
    {
        if (sortedPrices.Count == 0)
            return 0m;

        int mid = sortedPrices.Count / 2;
        return sortedPrices.Count % 2 == 1
            ? sortedPrices[mid]
            : Math.Round((sortedPrices[mid - 1] + sortedPrices[mid]) / 2m, 2);
    }

    private static List<string> BuildCandidateSearchQueries(string title, List<string>? tags, string? category)
    {
        var candidates = new List<string>();
        var primary = ExtractSearchKeyword(title, tags, category);
        AddDistinct(candidates, primary);

        if (tags is { Count: > 0 })
        {
            foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)).Take(2))
            {
                AddDistinct(candidates, tag.Trim());
            }
        }

        var coreWords = primary.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (coreWords.Length > 2)
        {
            AddDistinct(candidates, string.Join(' ', coreWords.Take(2)));
        }

        return candidates;

        static void AddDistinct(List<string> list, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !list.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(value);
            }
        }
    }

    private string ResolveHsCode(string title, string? category)
    {
        try
        {
            var matches = shipmentOrchestrator.SearchHsCodes($"{title} {category}", limit: 3);
            if (matches is { Count: > 0 } && !string.IsNullOrWhiteSpace(matches[0].Code))
                return matches[0].Code;
        }
        catch
        {
            // Fallback to general figurine/craft HS code
        }
        return "3926400000";
    }

    private async Task<(VariationShippingBenchmarkDto Benchmark, Dictionary<int, decimal> BucketCosts)> FetchShippingQuotesAsync(
        VariationPricingAgentRequestDto dto,
        string countryCode,
        string hsCode,
        decimal usdTryRate,
        int baseWeightGrams,
        IReadOnlyList<VariationSeed> seeds,
        CancellationToken cancellationToken)
    {
        var length = dto.LengthCm > 0 ? dto.LengthCm : 18.0;
        var width = dto.WidthCm > 0 ? dto.WidthCm : 14.0;
        var height = dto.HeightCm > 0 ? dto.HeightCm : 8.0;
        var postal = ResolveDefaultPostalCode(countryCode);

        // Group variation weights into up to 3 distinct 100g buckets (avoid redundant HTTP calls)
        var buckets = seeds
            .Select(s => RoundWeightBucket(s.EstimatedWeightGrams))
            .Append(RoundWeightBucket(baseWeightGrams))
            .Distinct()
            .OrderBy(w => w)
            .ToList();

        if (buckets.Count > 3)
        {
            buckets = [buckets.First(), buckets[buckets.Count / 2], buckets.Last()];
        }

        var bucketTasks = buckets.ToDictionary(
            grams => grams,
            grams =>
            {
                var scale = Math.Pow((double)grams / Math.Max(100, baseWeightGrams), 1.0 / 3.0);
                var query = new CarrierQuotesQuery
                {
                    CountryCode = countryCode,
                    PostalCode = postal,
                    WeightKg = Math.Round(grams / 1000.0, 3),
                    LengthCm = Math.Round(length * scale, 1),
                    WidthCm = Math.Round(width * scale, 1),
                    HeightCm = Math.Round(height * scale, 1),
                    HsCode = hsCode,
                    Quantity = 1,
                    DeclaredValueUsd = 20m,
                    UsdTryRate = usdTryRate
                };
                return SafeGetCarrierQuotesAsync(query, cancellationToken);
            });

        await Task.WhenAll(bucketTasks.Values);

        var bucketCosts = new Dictionary<int, decimal>();
        CarrierQuotesResponseDto? baseResponse = null;
        var baseBucket = RoundWeightBucket(baseWeightGrams);

        foreach (var (grams, task) in bucketTasks)
        {
            var resp = task.Result;
            var bestQuote = resp?.Quotes?.OrderBy(q => q.PriceUsd).FirstOrDefault();
            if (bestQuote is not null && bestQuote.PriceUsd > 0m)
            {
                bucketCosts[grams] = bestQuote.PriceUsd;
            }
            if (grams == baseBucket || baseResponse is null)
            {
                baseResponse = resp;
            }
        }

        var baseBest = baseResponse?.Quotes?.OrderBy(q => q.PriceUsd).FirstOrDefault();
        var basePriceUsd = baseBest?.PriceUsd > 0m ? baseBest.PriceUsd : 11.50m;
        var basePriceTry = baseBest?.PriceTry > 0m ? baseBest.PriceTry : Math.Round(basePriceUsd * usdTryRate, 2);

        var benchmark = new VariationShippingBenchmarkDto
        {
            TargetCountryCode = countryCode,
            SelectedCarrierKey = baseBest?.CarrierKey ?? "shipentegra",
            SelectedCarrierName = baseBest?.CarrierName ?? "ShipEntegra",
            SelectedServiceName = baseBest?.ServiceType ?? "Eco Express",
            BaseShippingCostUsd = basePriceUsd,
            BaseShippingCostTry = basePriceTry,
            CalculatedDesi = baseResponse?.CalculatedDesi ?? Math.Round((length * width * height) / 5000.0, 2),
            BillableWeightKg = baseResponse?.BillableWeightKg ?? Math.Round(baseWeightGrams / 1000.0, 2),
            EstimatedDays = baseBest?.EstimatedDays ?? "3-6 İş Günü",
            HsCodeUsed = hsCode,
            CarrierQuotes = (baseResponse?.Quotes ?? [])
                .OrderBy(q => q.PriceUsd)
                .Take(4)
                .Select(q => new AgentCarrierQuoteSummaryDto
                {
                    CarrierKey = q.CarrierKey,
                    CarrierName = q.CarrierName,
                    ServiceType = q.ServiceType,
                    PriceUsd = q.PriceUsd,
                    PriceTry = q.PriceTry,
                    EstimatedDays = q.EstimatedDays,
                    IsLive = q.IsLive
                })
                .ToList()
        };

        return (benchmark, bucketCosts);
    }

    private async Task<CarrierQuotesResponseDto?> SafeGetCarrierQuotesAsync(CarrierQuotesQuery query, CancellationToken cancellationToken)
    {
        try
        {
            return await shipmentOrchestrator.GetCarrierQuotesAsync(query, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static (
        bool IsSingleProduct,
        string VarType1,
        string VarValues1,
        bool EnableVar2,
        string VarType2,
        string VarValues2,
        List<VariationSeed> Seeds) BuildVariationPlan(VariationPricingAgentRequestDto dto, int baseWeightGrams)
    {
        if (dto.EnableVariations == false)
        {
            return (
                true,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                string.Empty,
                [new VariationSeed(SingleProductSeedKey, baseWeightGrams, 1.0, 1.0)]
            );
        }

        var varType1 = dto.VariationType1?.Trim() ?? string.Empty;
        var varValues1 = dto.VariationValues1?.Trim() ?? string.Empty;
        var enableVar2 = dto.EnableVariation2;
        var varType2 = dto.VariationType2?.Trim() ?? string.Empty;
        var varValues2 = dto.VariationValues2?.Trim() ?? string.Empty;

        var rawKeys = (dto.ExistingVariations ?? [])
            .Where(v => v.Active && !string.IsNullOrWhiteSpace(v.Key))
            .Select(v => v.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rawKeys.Count == 0 && !string.IsNullOrWhiteSpace(varValues1))
        {
            var v1List = SplitValues(varValues1);
            var v2List = enableVar2 && !string.IsNullOrWhiteSpace(varValues2)
                ? SplitValues(varValues2)
                : [];

            if (v2List.Count > 0)
            {
                foreach (var a in v1List)
                {
                    foreach (var b in v2List)
                        rawKeys.Add($"{a} / {b}");
                }
            }
            else
            {
                rawKeys.AddRange(v1List);
            }
        }

        if (rawKeys.Count == 0)
        {
            return (
                true,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                string.Empty,
                [new VariationSeed(SingleProductSeedKey, baseWeightGrams, 1.0, 1.0)]
            );
        }

        // First pass: extract explicit cm sizes across all variations for proportional scaling
        var parsedMeasurements = rawKeys.Select(ExtractDimensionCm).ToList();
        var validCms = parsedMeasurements.Where(c => c > 0).OrderBy(c => c).ToList();
        double medianCm = validCms.Count > 0 ? validCms[validCms.Count / 2] : 0;

        var seeds = new List<VariationSeed>(rawKeys.Count);
        for (int i = 0; i < rawKeys.Count; i++)
        {
            var key = rawKeys[i];
            var cm = parsedMeasurements[i];
            var weightScale = ResolveWeightScale(key, cm, medianCm);
            var qualityScale = ResolveQualityTierScale(key);
            var estWeight = Math.Clamp((int)Math.Round(baseWeightGrams * weightScale), 50, 25_000);

            seeds.Add(new VariationSeed(key, estWeight, weightScale, qualityScale));
        }

        return (false, varType1, varValues1, enableVar2, varType2, varValues2, seeds);
    }

    private static List<PreparedVariationModel> PrepareVariationBreakEvenModels(
        IReadOnlyList<VariationSeed> seeds,
        Dictionary<int, decimal> bucketCosts,
        decimal baseShippingUsd,
        decimal productionCostUsd,
        decimal targetMarginPct,
        bool includeOffsiteAds,
        decimal usdTryRate,
        VariationMarketBenchmarkDto market)
    {
        var variableRate = 0.1692m + (includeOffsiteAds ? 0.15m : 0m);
        var fixedFee = ((3.0m / usdTryRate) + 0.20m) * 1.20m;
        var marginRatio = targetMarginPct / 100m;
        var list = new List<PreparedVariationModel>(seeds.Count);

        var hasMarketAnchor = market.CompetitorCount > 0 && market.MedianPriceUsd > 0m;
        var marketMedian = market.MedianPriceUsd;

        foreach (var seed in seeds)
        {
            var shippingUsd = ResolveNearestBucketShipping(seed.EstimatedWeightGrams, bucketCosts, baseShippingUsd);
            var totalDirectCost = fixedFee + shippingUsd + productionCostUsd;

            var breakEven = Math.Round(totalDirectCost / (1m - variableRate), 2);
            var targetPrice = Math.Round(totalDirectCost / Math.Max(0.15m, 1m - variableRate - marginRatio), 2);
            var floorPrice = RoundUpPsychological(Math.Max(breakEven * 1.10m, breakEven + 1.00m));

            decimal suggestedPrice;
            if (hasMarketAnchor)
            {
                var marketScaled = marketMedian * (decimal)seed.WeightScale * (decimal)seed.QualityScale;
                var blended = (targetPrice * 0.50m) + (marketScaled * 0.50m);
                suggestedPrice = RoundUpPsychological(Math.Max(blended, floorPrice));
            }
            else
            {
                var tiered = targetPrice * (decimal)(1.0 + ((seed.WeightScale - 1.0) * 0.12)) * (decimal)seed.QualityScale;
                suggestedPrice = RoundUpPsychological(Math.Max(tiered, floorPrice));
            }

            var tierLabel = ResolveTierLabel(seed.WeightScale, seed.QualityScale, suggestedPrice, floorPrice);
            list.Add(new PreparedVariationModel(seed, shippingUsd, breakEven, floorPrice, suggestedPrice, productionCostUsd, tierLabel));
        }

        return list;
    }

    private async Task<(bool UsedAi, string ProviderUsed, string StrategySummary, Dictionary<string, LlmVariationOverride> Overrides)> TryRunLlmPricingAsync(
        VariationPricingAgentRequestDto dto,
        string keyword,
        VariationMarketBenchmarkDto market,
        VariationShippingBenchmarkDto shipping,
        IReadOnlyList<PreparedVariationModel> preparedRows,
        bool isSingleProduct,
        CancellationToken cancellationToken)
    {
        var apiKey = dto.ApiKey?.Trim() ?? string.Empty;
        var provider = string.IsNullOrWhiteSpace(dto.Provider) ? "Akıllı Fiyatlandırma Motoru" : dto.Provider.Trim();
        var model = ResolveDefaultModel(provider, dto.Model);

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.Equals(provider, "Offline", StringComparison.OrdinalIgnoreCase))
        {
            var offlineSummary = isSingleProduct
                ? (market.CompetitorCount > 0
                    ? $"'{keyword}' aramasında {market.CompetitorCount} Etsy rakibi (Medyan ${market.MedianPriceUsd:F2}) ve {shipping.SelectedCarrierName} kargo teklifi (${shipping.BaseShippingCostUsd:F2}) analiz edilerek tekil ürün için %{dto.TargetNetMarginPercent:F0} hedef net marj ve başabaş kalkanı ile fiyat belirlendi."
                    : $"{shipping.SelectedCarrierName} kargo teklifi (${shipping.BaseShippingCostUsd:F2}), Türkiye Etsy komisyon kesintileri ve hedef net marj baz alınarak zararsız tekil ürün fiyatı hesaplandı.")
                : (market.CompetitorCount > 0
                    ? $"'{keyword}' aramasında {market.CompetitorCount} Etsy rakibi (Medyan ${market.MedianPriceUsd:F2}) ve {shipping.SelectedCarrierName} kargo teklifi (${shipping.BaseShippingCostUsd:F2}) analiz edilerek tüm varyasyonlar %{dto.TargetNetMarginPercent:F0} hedef net marj ve başabaş kalkanı ile fiyatlandı."
                    : $"{shipping.SelectedCarrierName} kargo teklifi (${shipping.BaseShippingCostUsd:F2}), Türkiye Etsy komisyon kesintileri ve boyut/kalite ölçeği baz alınarak zararsız kademeli varyasyon fiyatları oluşturuldu.");

            return (false, "Akıllı Finansal Fiyat Motoru", offlineSummary, new Dictionary<string, LlmVariationOverride>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            const string singleSystemPrompt = """
                You are an expert Etsy Pricing Strategist and E-Commerce Profit Agent for Turkish Etsy sellers.
                This listing is a SINGLE-SKU product with NO variations.
                Your task is to determine ONE optimal USD sale price using:
                1. Real competitor market prices on Etsy (if available),
                2. Real international shipping quote from the logistics module,
                3. Etsy Turkey seller fees (16.92% + $0.31, plus 15% Offsite Ads if enabled) and the strict break-even floor price.

                CRITICAL RULES:
                - NEVER invent, suggest, or mention product variations, sizes, or colors.
                - NEVER recommend `price_usd` lower than `floor_price_usd`.
                - Keep Turkish concise `tier_label` (2-4 words), `reasoning` (1 short sentence), and `strategy_summary` (1-2 Turkish sentences).
                - Output ONLY strict JSON with no markdown code fences:
                {
                  "strategy_summary": "Turkish summary of single-product pricing logic (1-2 sentences)",
                  "price_usd": 24.90,
                  "tier_label": "Hedef Net Marj",
                  "reasoning": "Kargo $X ve Etsy kesintileri sonrası %Y net marj bırakır."
                }
                """;

            const string variationSystemPrompt = """
                You are an expert Etsy Pricing Strategist and E-Commerce Profit Agent for Turkish Etsy sellers.
                Your task is to determine optimal USD prices for every product variation using:
                1. Real competitor market prices on Etsy (if available),
                2. Real international shipping quotes from the logistics module (per variation weight/desi),
                3. Etsy Turkey seller fees (16.92% + $0.31, plus 15% Offsite Ads if enabled) and strict break-even floor prices.

                CRITICAL RULES:
                - NEVER recommend a price_usd lower than that variation's `floor_price_usd`.
                - Use strategic Etsy tiering: entry/small options can act as attractive CTR anchor prices (still strictly >= `floor_price_usd`), while larger sizes or premium finishes step up smoothly to cover extra cargo weight and protect target net profit margin.
                - Keep Turkish concise `tier_label` (2-4 words) and `reasoning` (1 short sentence).
                - Output ONLY strict JSON with no markdown code fences:
                {
                  "strategy_summary": "Turkish summary of overall pricing logic (1-2 sentences)",
                  "variations": [
                    {
                      "key": "exact variation key string",
                      "price_usd": 24.90,
                      "tier_label": "Giriş / Yüksek CTR",
                      "reasoning": "Kargo $X ve Etsy kesintileri sonrası %Y net marj bırakır."
                    }
                  ]
                }
                """;

            var singleRow = preparedRows.FirstOrDefault();
            object promptModel = isSingleProduct
                ? new
                {
                    mode = "single_product_no_variations",
                    product = new
                    {
                        title = dto.Title,
                        category = dto.Category,
                        materials = dto.Materials,
                        target_country = shipping.TargetCountryCode,
                        target_net_margin_pct = dto.TargetNetMarginPercent,
                        production_cost_usd = dto.ProductionCostUsd,
                        include_offsite_ads = dto.IncludeOffsiteAds
                    },
                    market_benchmark = new
                    {
                        search_keyword = keyword,
                        competitor_count = market.CompetitorCount,
                        min_usd = market.MinPriceUsd,
                        median_usd = market.MedianPriceUsd,
                        average_usd = market.AveragePriceUsd,
                        p75_usd = market.P75PriceUsd,
                        max_usd = market.MaxPriceUsd,
                        free_shipping_pct = market.FreeShippingRatioPct,
                        free_shipping_median_usd = market.FreeShippingMedianUsd,
                        paid_shipping_median_usd = market.PaidShippingMedianUsd,
                        top_competitors = market.TopCompetitors.Select(c => new { c.Title, c.PriceUsd, c.ShopSales, c.HasFreeShipping })
                    },
                    shipping_benchmark = new
                    {
                        carrier = shipping.SelectedCarrierName,
                        service = shipping.SelectedServiceName,
                        base_shipping_usd = shipping.BaseShippingCostUsd,
                        hs_code = shipping.HsCodeUsed
                    },
                    single_sku_guardrails = new
                    {
                        estimated_weight_grams = singleRow?.Seed.EstimatedWeightGrams ?? dto.BaseWeightGrams,
                        shipping_cost_usd = singleRow?.ShippingUsd ?? shipping.BaseShippingCostUsd,
                        break_even_usd = singleRow?.BreakEvenUsd ?? 0m,
                        floor_price_usd = singleRow?.FloorPriceUsd ?? 0m,
                        initial_suggested_usd = singleRow?.SuggestedPriceUsd ?? 0m
                    }
                }
                : new
                {
                    product = new
                    {
                        title = dto.Title,
                        category = dto.Category,
                        materials = dto.Materials,
                        target_country = shipping.TargetCountryCode,
                        target_net_margin_pct = dto.TargetNetMarginPercent,
                        production_cost_usd = dto.ProductionCostUsd,
                        include_offsite_ads = dto.IncludeOffsiteAds
                    },
                    market_benchmark = new
                    {
                        search_keyword = keyword,
                        competitor_count = market.CompetitorCount,
                        min_usd = market.MinPriceUsd,
                        median_usd = market.MedianPriceUsd,
                        average_usd = market.AveragePriceUsd,
                        p75_usd = market.P75PriceUsd,
                        max_usd = market.MaxPriceUsd,
                        free_shipping_pct = market.FreeShippingRatioPct,
                        free_shipping_median_usd = market.FreeShippingMedianUsd,
                        paid_shipping_median_usd = market.PaidShippingMedianUsd,
                        top_competitors = market.TopCompetitors.Select(c => new { c.Title, c.PriceUsd, c.ShopSales, c.HasFreeShipping })
                    },
                    shipping_benchmark = new
                    {
                        carrier = shipping.SelectedCarrierName,
                        service = shipping.SelectedServiceName,
                        base_shipping_usd = shipping.BaseShippingCostUsd,
                        hs_code = shipping.HsCodeUsed
                    },
                    variations = preparedRows.Take(40).Select(r => new
                    {
                        key = r.Seed.Key,
                        estimated_weight_grams = r.Seed.EstimatedWeightGrams,
                        shipping_cost_usd = r.ShippingUsd,
                        break_even_usd = r.BreakEvenUsd,
                        floor_price_usd = r.FloorPriceUsd,
                        initial_suggested_usd = r.SuggestedPriceUsd
                    })
                };

            var systemPrompt = isSingleProduct ? singleSystemPrompt : variationSystemPrompt;
            var userPrompt = JsonSerializer.Serialize(promptModel);
            var (success, content, _) = await aiClient.GenerateOptimizationJsonAsync(
                systemPrompt, userPrompt, model, apiKey, cancellationToken);

            if (success && !string.IsNullOrWhiteSpace(content))
            {
                var parsed = ParseLlmPricingResponse(content, isSingleProduct);
                if (parsed.Overrides.Count > 0)
                {
                    var label = model.Contains(provider, StringComparison.OrdinalIgnoreCase)
                        ? model
                        : $"{provider} ({model})";

                    var fallbackSummary = isSingleProduct
                        ? $"{label} ile tekil ürün rakip verisi ve {shipping.SelectedCarrierName} kargo tarifesine göre fiyatlandı."
                        : $"{label} ile {preparedRows.Count} varyasyon rakip verisi ve {shipping.SelectedCarrierName} kargo tarifesine göre fiyatlandı.";

                    var summary = string.IsNullOrWhiteSpace(parsed.Summary)
                        ? fallbackSummary
                        : parsed.Summary;

                    return (true, label, summary, parsed.Overrides);
                }
            }
        }
        catch
        {
            // Fall back smoothly to deterministic financial model if LLM request fails
        }

        return (
            false,
            $"{provider} Yedeği (Finansal Motor)",
            isSingleProduct
                ? $"{shipping.SelectedCarrierName} kargo (${shipping.BaseShippingCostUsd:F2}) ve başabaş kalkanı ile güvenli tekil ürün fiyatı uygulandı."
                : $"{shipping.SelectedCarrierName} kargo (${shipping.BaseShippingCostUsd:F2}) ve başabaş kalkanı ile güvenli kademeli fiyatlandırma uygulandı.",
            new Dictionary<string, LlmVariationOverride>(StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolveDefaultModel(string provider, string? requestedModel)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
            return requestedModel.Trim();

        return provider.Trim().ToLowerInvariant() switch
        {
            "deepseek" => "deepseek-chat",
            "openai" => "gpt-4o-mini",
            "claude" => "claude-3-7-sonnet-20250219",
            "grok" => "grok-3",
            _ => "gemini-2.5-flash"
        };
    }

    private static List<PricedVariationRowDto> FinalizePricedVariations(
        IReadOnlyList<PreparedVariationModel> preparedRows,
        Dictionary<string, LlmVariationOverride> overrides,
        bool includeOffsiteAds,
        decimal usdTryRate)
    {
        var results = new List<PricedVariationRowDto>(preparedRows.Count);
        foreach (var row in preparedRows)
        {
            var finalPrice = row.SuggestedPriceUsd;
            var tierLabel = row.TierLabel;
            var reasoning = $"Kargo ${row.ShippingUsd:F2} ve kesintiler sonrası güvenli marj";

            if (overrides.TryGetValue(row.Seed.Key, out var llm))
            {
                finalPrice = llm.PriceUsd > 0m
                    ? RoundUpPsychological(Math.Max(llm.PriceUsd, row.FloorPriceUsd))
                    : row.FloorPriceUsd;
                if (!string.IsNullOrWhiteSpace(llm.TierLabel)) tierLabel = llm.TierLabel;
                if (!string.IsNullOrWhiteSpace(llm.Reasoning)) reasoning = llm.Reasoning;
            }

            var (feesUsd, netProfitUsd, netMarginPct) = CalculateEtsyFeesAndNet(
                finalPrice, row.ShippingUsd, row.ProductionCostUsd, usdTryRate, includeOffsiteAds);

            results.Add(new PricedVariationRowDto
            {
                Key = row.Seed.Key,
                RecommendedPriceUsd = finalPrice,
                EstimatedWeightGrams = row.Seed.EstimatedWeightGrams,
                EstimatedShippingCostUsd = Math.Round(row.ShippingUsd, 2),
                EtsyFeesUsd = Math.Round(feesUsd, 2),
                ProductionCostUsd = Math.Round(row.ProductionCostUsd, 2),
                NetProfitUsd = Math.Round(netProfitUsd, 2),
                NetMarginPercent = Math.Round(netMarginPct, 1),
                BreakEvenPriceUsd = row.BreakEvenUsd,
                PricingTierLabel = tierLabel,
                Reasoning = reasoning
            });
        }

        return results;
    }

    private static (string Summary, Dictionary<string, LlmVariationOverride> Overrides) ParseLlmPricingResponse(
        string rawContent,
        bool isSingleProduct)
    {
        var clean = rawContent.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) clean = clean[7..];
        if (clean.StartsWith("```")) clean = clean[3..];
        if (clean.EndsWith("```")) clean = clean[..^3];
        clean = clean.Trim();

        var firstBrace = clean.IndexOf('{');
        var lastBrace = clean.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            clean = clean[firstBrace..(lastBrace + 1)];
        }

        var overrides = new Dictionary<string, LlmVariationOverride>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(clean);
        var root = doc.RootElement;

        var summary = root.TryGetProperty("strategy_summary", out var ss) && ss.ValueKind == JsonValueKind.String
            ? (ss.GetString() ?? string.Empty).Trim()
            : string.Empty;

        if (isSingleProduct)
        {
            var singlePrice = ReadDecimalProperty(root, "price_usd");
            if (singlePrice > 0m)
            {
                var label = root.TryGetProperty("tier_label", out var lEl) && lEl.ValueKind == JsonValueKind.String
                    ? (lEl.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                var reason = root.TryGetProperty("reasoning", out var rEl) && rEl.ValueKind == JsonValueKind.String
                    ? (rEl.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                overrides[SingleProductSeedKey] = new LlmVariationOverride(singlePrice, label, reason);
            }
        }

        if (root.TryGetProperty("variations", out var varArr) && varArr.ValueKind == JsonValueKind.Array)
        {
            LlmVariationOverride? firstSingleFallback = null;
            foreach (var item in varArr.EnumerateArray())
            {
                var key = item.TryGetProperty("key", out var kEl) && kEl.ValueKind == JsonValueKind.String
                    ? (kEl.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var price = ReadDecimalProperty(item, "price_usd");

                var label = item.TryGetProperty("tier_label", out var lEl) && lEl.ValueKind == JsonValueKind.String
                    ? (lEl.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                var reason = item.TryGetProperty("reasoning", out var rEl) && rEl.ValueKind == JsonValueKind.String
                    ? (rEl.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                if (price > 0m)
                {
                    var ov = new LlmVariationOverride(price, label, reason);
                    firstSingleFallback ??= ov;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        overrides[key] = ov;
                    }
                }
            }

            if (isSingleProduct && !overrides.ContainsKey(SingleProductSeedKey) && firstSingleFallback is not null)
            {
                overrides[SingleProductSeedKey] = firstSingleFallback;
            }
        }

        return (summary, overrides);
    }

    private static decimal ReadDecimalProperty(JsonElement obj, string propName)
    {
        if (!obj.TryGetProperty(propName, out var pEl))
            return 0m;

        if (pEl.ValueKind == JsonValueKind.Number && pEl.TryGetDecimal(out var dec))
            return dec;

        if (pEl.ValueKind == JsonValueKind.String &&
            decimal.TryParse(pEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDec))
            return parsedDec;

        return 0m;
    }

    private static (decimal TotalFeesUsd, decimal NetProfitUsd, decimal NetMarginPct) CalculateEtsyFeesAndNet(
        decimal salePriceUsd,
        decimal shippingCostUsd,
        decimal productionCostUsd,
        decimal usdTryRate,
        bool includeOffsiteAds)
    {
        if (salePriceUsd <= 0m) return (0m, -shippingCostUsd - productionCostUsd, 0m);

        var txFee = salePriceUsd * 0.065m;
        var payFee = (salePriceUsd * 0.065m) + (3.0m / usdTryRate);
        var regFee = salePriceUsd * 0.011m;
        const decimal listingFee = 0.20m;
        var vatOnFees = (txFee + payFee + regFee + listingFee) * 0.20m;
        var offsiteFee = includeOffsiteAds ? salePriceUsd * 0.15m : 0m;

        var totalFees = txFee + payFee + regFee + listingFee + vatOnFees + offsiteFee;
        var netProfit = salePriceUsd - totalFees - shippingCostUsd - productionCostUsd;
        var marginPct = (netProfit / salePriceUsd) * 100m;
        return (totalFees, netProfit, marginPct);
    }

    private static decimal ResolveNearestBucketShipping(
        int estimatedWeightGrams,
        Dictionary<int, decimal> bucketCosts,
        decimal fallbackShipping)
    {
        if (bucketCosts.Count == 0) return fallbackShipping;
        var nearestKey = bucketCosts.Keys.OrderBy(k => Math.Abs(k - estimatedWeightGrams)).First();
        return bucketCosts[nearestKey];
    }

    private static string ExtractSearchKeyword(string title, List<string>? tags, string? category)
    {
        var primaryChunk = title
            .Split(['|', '-', '—', '/', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => s.Length >= 3) ?? title.Trim();

        var words = primaryChunk
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2 && !StopWords.Contains(w))
            .Take(4)
            .ToList();

        if (words.Count >= 2) return string.Join(' ', words);

        var fallbackTag = tags?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        if (!string.IsNullOrWhiteSpace(fallbackTag)) return fallbackTag.Trim();

        return !string.IsNullOrWhiteSpace(primaryChunk) ? primaryChunk : (category ?? "handmade gift");
    }

    private static double ExtractDimensionCm(string key)
    {
        var match = DimensionRegex.Match(key);
        if (!match.Success) return 0;
        if (!double.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            return 0;

        var unit = match.Groups[2].Value.ToLowerInvariant();
        if (unit.StartsWith("in") || unit == "\"") return val * 2.54;
        if (unit == "mm") return val / 10.0;
        return val;
    }

    private static double ResolveWeightScale(string key, double cm, double medianCm)
    {
        double sizeScale = 1.0;
        if (cm > 0 && medianCm > 0)
        {
            sizeScale = Math.Clamp(Math.Pow(cm / medianCm, 1.15), 0.65, 2.8);
        }
        else
        {
            var lower = key.ToLowerInvariant();
            if (lower.Contains("xxl") || lower.Contains("jumbo") || lower.Contains("giant") || lower.Contains("mega"))
                sizeScale = 1.85;
            else if (Regex.IsMatch(lower, @"\b(xl)\b"))
                sizeScale = 1.55;
            else if (Regex.IsMatch(lower, @"\b(large|l|büyük)\b"))
                sizeScale = 1.30;
            else if (Regex.IsMatch(lower, @"\b(small|s|küçük|mini|xs)\b"))
                sizeScale = 0.80;
        }

        var packMatch = PackCountRegex.Match(key);
        if (packMatch.Success)
        {
            var rawCount = packMatch.Groups[1].Success ? packMatch.Groups[1].Value : packMatch.Groups[2].Value;
            if (int.TryParse(rawCount, out var count) && count is >= 2 and <= 20)
            {
                sizeScale *= 1.0 + ((count - 1) * 0.70);
            }
        }

        return Math.Clamp(sizeScale, 0.55, 3.50);
    }

    private static double ResolveQualityTierScale(string key)
    {
        var lower = key.ToLowerInvariant();
        if (lower.Contains("custom") || lower.Contains("kişisel") || lower.Contains("personal") || lower.Contains("led"))
            return 1.18;
        if (lower.Contains("rainbow") || lower.Contains("gökkuşa") || lower.Contains("silk") || lower.Contains("ipek") ||
            lower.Contains("metallic") || lower.Contains("chrome") || lower.Contains("krom") || lower.Contains("glow") ||
            lower.Contains("gold") || lower.Contains("altın") || lower.Contains("bronze") || lower.Contains("bronz") ||
            lower.Contains("dual"))
            return 1.10;
        return 1.0;
    }

    private static string ResolveTierLabel(double weightScale, double qualityScale, decimal price, decimal floorPrice)
    {
        if (weightScale >= 1.25) return "Büyük Boy / Yüksek Sepet";
        if (qualityScale > 1.05) return "Özel Seri / Yüksek Marj";
        if (weightScale < 0.90 || price <= floorPrice + 1.5m) return "Giriş / CTR Fiyatı";
        return "Standart Hedef Marj";
    }

    private static decimal RoundUpPsychological(decimal rawPrice)
    {
        if (rawPrice <= 0m) return 9.90m;
        var whole = Math.Floor(rawPrice);
        var candidate = whole + 0.90m;
        return candidate >= rawPrice ? candidate : whole + 1.90m;
    }

    private static int RoundWeightBucket(int weightGrams)
        => Math.Max(100, (int)Math.Round(weightGrams / 100.0) * 100);

    private static string ResolveDefaultPostalCode(string countryCode) => countryCode switch
    {
        "DE" => "10115",
        "GB" => "SW1A 1AA",
        "CA" => "M5V 2T6",
        "AU" => "2000",
        "FR" => "75001",
        _ => "10001"
    };

    private static List<string> SplitValues(string raw)
        => raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    private sealed record VariationSeed(string Key, int EstimatedWeightGrams, double WeightScale, double QualityScale);

    private sealed record PreparedVariationModel(
        VariationSeed Seed,
        decimal ShippingUsd,
        decimal BreakEvenUsd,
        decimal FloorPriceUsd,
        decimal SuggestedPriceUsd,
        decimal ProductionCostUsd,
        string TierLabel);

    private sealed record LlmVariationOverride(decimal PriceUsd, string TierLabel, string Reasoning);
}
