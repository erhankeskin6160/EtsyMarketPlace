namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;

public sealed class AiListingOptimizationUseCase : IAiListingOptimizerUseCase
{
    private readonly IGeminiListingApiClient _geminiClient;
    private readonly ListingOptimizationService _ruleOptimizer;
    private readonly IShopSettingsRepository _settingsRepo;

    public AiListingOptimizationUseCase(
        IGeminiListingApiClient geminiClient,
        ListingOptimizationService ruleOptimizer,
        IShopSettingsRepository settingsRepo)
    {
        _geminiClient = geminiClient ?? throw new ArgumentNullException(nameof(geminiClient));
        _ruleOptimizer = ruleOptimizer ?? throw new ArgumentNullException(nameof(ruleOptimizer));
        _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));
    }

    public async Task<OptimizeListingResultDto> OptimizeListingAsync(
        string listingId,
        string shopId,
        OptimizeListingCommandDto command,
        CancellationToken cancellationToken = default)
    {
        var title = command?.Title ?? string.Empty;
        var description = command?.Description ?? string.Empty;
        var tags = command?.Tags ?? Array.Empty<string>();
        var targetKw = !string.IsNullOrWhiteSpace(command?.TargetKeyword)
            ? command.TargetKeyword
            : (!string.IsNullOrWhiteSpace(title) ? title : listingId);

        var input = new ListingOptimizationInput(title, description, tags, targetKw, command?.DescriptionStyle ?? "Storytelling");

        var apiKey = command?.ApiKey?.Trim() ?? string.Empty;
        var requestedProvider = command?.Provider?.Trim();
        var requestedModel = command?.Model?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(requestedProvider))
        {
            try
            {
                var savedJson = await _settingsRepo.GetAiSettingsJsonAsync(shopId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(savedJson))
                {
                    using var settingsDoc = JsonDocument.Parse(savedJson);
                    var sRoot = settingsDoc.RootElement;
                    if (string.IsNullOrWhiteSpace(requestedProvider) &&
                        sRoot.TryGetProperty("provider", out var pEl) &&
                        pEl.ValueKind == JsonValueKind.String)
                    {
                        requestedProvider = pEl.GetString()?.Trim();
                    }

                    var prov = requestedProvider ?? "Gemini";
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        var keyProp = prov.ToLowerInvariant() switch
                        {
                            "deepseek" => "deepSeekApiKey",
                            "openai" => "openAiApiKey",
                            "claude" => "claudeApiKey",
                            "grok" => "grokApiKey",
                            _ => "geminiApiKey"
                        };
                        if (sRoot.TryGetProperty(keyProp, out var kEl) && kEl.ValueKind == JsonValueKind.String)
                        {
                            apiKey = kEl.GetString()?.Trim() ?? string.Empty;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(requestedModel))
                    {
                        var modelProp = prov.ToLowerInvariant() switch
                        {
                            "deepseek" => "deepSeekModel",
                            "openai" => "openAiModel",
                            "claude" => "claudeModel",
                            "grok" => "grokModel",
                            _ => "geminiModel"
                        };
                        if (sRoot.TryGetProperty(modelProp, out var mEl) && mEl.ValueKind == JsonValueKind.String)
                        {
                            requestedModel = mEl.GetString()?.Trim();
                        }
                    }
                }
            }
            catch { }
        }
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
            var activeProviderName = string.IsNullOrWhiteSpace(requestedProvider) ? "Gemini" : requestedProvider;
            var isGeminiProvider = activeProviderName.Contains("Gemini", StringComparison.OrdinalIgnoreCase);
            var rawModel = !string.IsNullOrWhiteSpace(requestedModel)
                ? requestedModel
                : activeProviderName.ToLowerInvariant() switch
                {
                    "deepseek" => "DeepSeek-V4-Flash",
                    "openai" => "gpt-4o",
                    "claude" => "claude-3-7-sonnet-20250219",
                    "grok" => "grok-3",
                    _ => "gemini-2.5-flash"
                };
            var geminiModel = isGeminiProvider
                ? EtsyAiModelNormalizer.NormalizeGeminiTextModel(rawModel)
                : rawModel;


            var systemInstruction = ListingDraftInstructionBuilder.BuildSystemInstruction();
            var userPrompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

            var (success, candidateText, error) = await _geminiClient.GenerateOptimizationJsonAsync(
                systemInstruction,
                userPrompt,
                geminiModel,
                apiKey,
                cancellationToken);

            if (!success || string.IsNullOrWhiteSpace(candidateText))
            {
                throw new InvalidOperationException(error ?? "Google Gemini boş yanıt döndürdü.");
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

            resolvedProvider = $"{activeProviderName} (Canlı API)";
            resolvedModel = geminiModel;
        }
        else
        {
            if (!isOfflineRequested && !string.IsNullOrWhiteSpace(requestedProvider) && requestedProvider.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                riskWarnings.Add("CanlÄ± Gemini API anahtarÄ± girilmediÄŸi iÃ§in yerel kural motoru (Fallback) kullanÄ±ldÄ±.");
            }

            var offlineResult = _ruleOptimizer.Optimize(input);
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
            shopId,
            listingId,
            title,
            currentScore,
            optimizedScore,
            status,
            resolvedProvider,
            resolvedModel,
            JsonSerializer.Serialize(auditData)), cancellationToken);

        var msg = resolvedProvider.Contains("Canlı", StringComparison.OrdinalIgnoreCase)
            ? "Listing başarıyla canlı Google Gemini AI ile optimize edildi ve yerel veritabanına kaydedildi."
            : "Listing başarıyla yerel kural motoru ile optimize edildi ve yerel veritabanına kaydedildi.";

        return new OptimizeListingResultDto(
            Success: true,
            ListingId: listingId,
            CurrentSeoScore: currentScore,
            OptimizedSeoScore: optimizedScore,
            SeoScoreBefore: currentScore,
            SeoScoreAfter: optimizedScore,
            OptimizedTitle: bestTitle,
            SuggestedTitle: bestTitle,
            TitleSuggestions: titleSuggestions,
            OptimizedTags: tagSuggestions.Take(13).ToList(),
            TagSuggestions: tagSuggestions.Take(13).ToList(),
            MaterialSuggestions: materialSuggestions,
            OptimizedDescription: descriptionDraft,
            DescriptionDraft: descriptionDraft,
            Critique: seoCritique,
            SeoCritique: seoCritique,
            MissingTerms: Array.Empty<string>(),
            RiskWarnings: riskWarnings,
            Checklist: checklist,
            Status: status,
            AiModel: resolvedModel,
            Provider: resolvedProvider,
            Model: resolvedModel,
            Message: msg);
    }
}
