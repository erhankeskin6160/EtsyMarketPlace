namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Viral3DModels.Interfaces;
using EtsyMarketPlace.Application.Viral3DModels.Services;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;
using SimilarProductsWinForms.Models;

internal sealed class ActiveAiShopNicheAnalyzer : IShopNicheAnalyzer
{
    private readonly Func<AiOptimizationSettings> _settingsProvider;

    public ActiveAiShopNicheAnalyzer(Func<AiOptimizationSettings>? settingsProvider = null)
    {
        _settingsProvider = settingsProvider ?? AiOptimizationSettingsStore.Load;
    }

    public async Task<ShopNicheProfile> AnalyzeShopNicheAsync(
        string shopName,
        IEnumerable<ShopListingItem> listings,
        CancellationToken ct = default)
    {
        var settings = _settingsProvider();
        var items = listings?.ToList() ?? [];

        if (items.Count == 0)
        {
            return ShopNicheProfile.CreateDefaultFigureAndToy(shopName);
        }

        // If offline or no AI configured, fallback to Heuristic
        if (settings.IsOffline || !settings.HasAnyAiProvider)
        {
            return HeuristicShopNicheClassifier.Classify(shopName, items, "Yerel NLP Motoru (Çevrimdışı)");
        }

        string providerName = settings.GetActiveBadgeText();

        // Build compact representation of listings for LLM prompt
        var sampleListings = items.Take(25).Select(it => new
        {
            title = it.Title,
            category = it.Category,
            tags = it.Tags?.Take(8).ToList()
        });

        string promptJson = JsonSerializer.Serialize(sampleListings);

        string systemPrompt =
            "Sen uzman bir e-ticaret ve Etsy 3D baskı pazar analistisin. " +
            "Kullanıcının Etsy mağazasındaki ürün listesini inceleyip mağazanın ana nişini ve 3D baskı (STL) ürün öneri profilini çıkar. " +
            "Yanıtını YALNIZCA geçerli bir JSON objesi olarak ver, markdown backtick (```json) bloğu KULLANMA.\n\n" +
            "JSON Şeması:\n" +
            "{\n" +
            "  \"primary_niche\": \"string (örn: 🎮 Eklemli Figür, Oyuncak & Fidget Modeller veya 🌿 Modern Ev & Geometrik Saksı Tasarımları vb.)\",\n" +
            "  \"target_audience\": \"string (hedef kitle tanımı)\",\n" +
            "  \"secondary_niches\": [\"string\", \"string\"],\n" +
            "  \"affinity_keywords\": [\"figure\", \"toy\", \"dragon\", \"articulated\", \"jointed\", \"fidget\", ...],\n" +
            "  \"ai_reasoning\": \"string (mağazanın neden bu kategoride olduğu ve müşterilerinin hangi 3D STL modellerini alacağına dair stratejik tavsiye)\",\n" +
            "  \"confidence_score\": 95\n" +
            "}";

        string userPrompt = $"Mağaza Adı: {shopName}\nİncelenen Listing Verileri:\n{promptJson}";

        try
        {
            string aiResponse = "";

            if (settings.UseGemini)
            {
                aiResponse = await AiProviderCaller.CallGeminiAsync(
                    systemPrompt, userPrompt, settings.GeminiApiKey, settings.GeminiModel, 2048, 0.3, ct);
            }
            else if (settings.UseOpenAi)
            {
                aiResponse = await AiProviderCaller.CallOpenAiAsync(
                    systemPrompt, userPrompt, settings.OpenAiApiKey, settings.OpenAiModel, 2048, 0.3, ct);
            }
            else if (settings.UseClaude)
            {
                aiResponse = await AiProviderCaller.CallClaudeAsync(
                    systemPrompt, userPrompt, settings.ClaudeApiKey, settings.ClaudeModel, 2048, 0.3, ct);
            }
            else if (settings.UseDeepSeek)
            {
                aiResponse = await AiProviderCaller.CallDeepSeekAsync(
                    systemPrompt, userPrompt, settings.DeepSeekApiKey, settings.DeepSeekModel, 2048, 0.3, ct);
            }
            else if (settings.UseGrok)
            {
                aiResponse = await AiProviderCaller.CallGrokAsync(
                    systemPrompt, userPrompt, settings.GrokApiKey, settings.GrokModel, 2048, 0.3, ct);
            }

            if (!string.IsNullOrWhiteSpace(aiResponse) && !aiResponse.StartsWith("⚠️"))
            {
                var profile = ParseAiResponse(aiResponse, shopName, items.Count, providerName);
                if (profile != null)
                {
                    return profile;
                }
            }
        }
        catch
        {
            // Failover to heuristic on any network/API exception
        }

        return HeuristicShopNicheClassifier.Classify(shopName, items, $"{providerName} (Yedek NLP Devrede)");
    }

    private static ShopNicheProfile? ParseAiResponse(string json, string shopName, int count, string providerName)
    {
        try
        {
            string clean = json.Trim();
            if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(7);
            }
            if (clean.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(3);
            }
            if (clean.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - 3);
            }
            clean = clean.Trim();

            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            string primaryNiche = root.TryGetProperty("primary_niche", out var pn) ? pn.GetString() ?? "" : "";
            string targetAudience = root.TryGetProperty("target_audience", out var ta) ? ta.GetString() ?? "" : "";
            string reasoning = root.TryGetProperty("ai_reasoning", out var ar) ? ar.GetString() ?? "" : "";
            int confidence = root.TryGetProperty("confidence_score", out var cs) ? cs.GetInt32() : 92;

            var secondaryNiches = new List<string>();
            if (root.TryGetProperty("secondary_niches", out var sn) && sn.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in sn.EnumerateArray())
                {
                    string? s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) secondaryNiches.Add(s);
                }
            }

            var affinityKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("affinity_keywords", out var ak) && ak.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in ak.EnumerateArray())
                {
                    string? s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) affinityKeywords.Add(s);
                }
            }

            if (string.IsNullOrWhiteSpace(primaryNiche)) return null;

            return new ShopNicheProfile
            {
                ShopName = shopName,
                PrimaryNiche = primaryNiche,
                TargetAudience = !string.IsNullOrWhiteSpace(targetAudience) ? targetAudience : "Genel Etsy Alıcıları",
                SecondaryNiches = secondaryNiches,
                AffinityKeywords = affinityKeywords,
                ActiveAiProviderName = providerName,
                AiReasoning = reasoning,
                ConfidenceScore = Math.Min(99, Math.Max(70, confidence)),
                AnalyzedListingCount = count,
                AnalyzedAtUtc = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }
}
