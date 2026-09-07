namespace SimilarProductsWinForms.Services;

using System;
using SimilarProductsWinForms.Models;

/// <summary>
/// Görev karmaşıklığına göre en uygun AI modelini ve provider'ı seçen akıllı yönlendirici.
/// Tier-1 (Hızlı/Ucuz), Tier-2 (Dengeli), Tier-3 (Premium/Reasoning) katmanlarıyla çalışır.
/// Otomatik fallback mekanizması içerir.
/// </summary>
internal sealed class AiModelRouter
{
    /// <summary>Görev karmaşıklık seviyesi.</summary>
    public enum TaskComplexity
    {
        /// <summary>Basit sınıflandırma, metadata tarama, hızlı triage.</summary>
        Simple,
        /// <summary>Standart görsel denetim, SEO analizi, listing optimizasyonu.</summary>
        Standard,
        /// <summary>Derin kök neden analizi, stratejik rapor, What-If simülasyonu.</summary>
        Complex
    }

    /// <summary>Yönlendirme sonucu — hangi provider, hangi model, hangi prompt stratejisi.</summary>
    public sealed record RoutingDecision(
        string Provider,            // "OpenAI", "Gemini", "Claude"
        string ModelId,             // Gerçek model adı (ör: "gpt-4o-mini", "gemini-2.5-flash", "claude-sonnet-5")
        string VisionModelId,       // Görsel analiz modeli (varsa)
        TaskComplexity Tier,
        bool SupportsVision,
        bool SupportsStructuredOutput,
        string PromptStyleHint,     // "conversational", "xml_tags", "structured_markdown"
        int MaxOutputTokens,
        double Temperature);

    /// <summary>
    /// Görev karmaşıklığını değerlendirip en uygun modeli seçer.
    /// </summary>
    public static RoutingDecision Route(TaskComplexity complexity, AiOptimizationSettings settings, bool requiresVision = false)
    {
        // 1. Kullanılabilir provider'ları belirle (öncelik sırası: Claude → OpenAI → Gemini)
        bool hasClaude = !string.IsNullOrWhiteSpace(settings.ClaudeApiKey);
        bool hasOpenAi = !string.IsNullOrWhiteSpace(settings.OpenAiApiKey);
        bool hasGemini = !string.IsNullOrWhiteSpace(settings.GeminiApiKey);

        // 2. Karmaşıklığa göre yönlendir
        return complexity switch
        {
            TaskComplexity.Simple => RouteSimple(settings, hasClaude, hasOpenAi, hasGemini),
            TaskComplexity.Standard => RouteStandard(settings, hasClaude, hasOpenAi, hasGemini, requiresVision),
            TaskComplexity.Complex => RouteComplex(settings, hasClaude, hasOpenAi, hasGemini, requiresVision),
            _ => RouteStandard(settings, hasClaude, hasOpenAi, hasGemini, requiresVision)
        };
    }

    /// <summary>
    /// Görev tipine göre karmaşıklık seviyesini otomatik belirler.
    /// </summary>
    public static TaskComplexity ClassifyTask(string taskType)
    {
        return taskType.ToLowerInvariant() switch
        {
            "metadata_scan" or "tag_check" or "price_check" or "triage" => TaskComplexity.Simple,
            "vision_audit" or "seo_analysis" or "listing_optimize" or "funnel_analysis" => TaskComplexity.Standard,
            "root_cause" or "diagnostic_report" or "strategic_plan" or "what_if" or "store_doctor_chat" => TaskComplexity.Complex,
            _ => TaskComplexity.Standard
        };
    }

    // --- Tier 1: Hızlı & Ucuz ---
    private static RoutingDecision RouteSimple(AiOptimizationSettings s, bool hasClaude, bool hasOpenAi, bool hasGemini)
    {
        // Gemini Flash en hızlı ve ucuz seçenek
        if (hasGemini)
        {
            return new RoutingDecision(
                Provider: "Gemini",
                ModelId: "gemini-2.5-flash",
                VisionModelId: "gemini-2.5-flash",
                Tier: TaskComplexity.Simple,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "structured_markdown",
                MaxOutputTokens: 400,
                Temperature: 0.3);
        }

        if (hasOpenAi)
        {
            return new RoutingDecision(
                Provider: "OpenAI",
                ModelId: "gpt-4o-mini",
                VisionModelId: "gpt-4o-mini",
                Tier: TaskComplexity.Simple,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "conversational",
                MaxOutputTokens: 400,
                Temperature: 0.3);
        }

        if (hasClaude)
        {
            return new RoutingDecision(
                Provider: "Claude",
                ModelId: "claude-haiku-4.5",
                VisionModelId: "claude-haiku-4.5",
                Tier: TaskComplexity.Simple,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "xml_tags",
                MaxOutputTokens: 400,
                Temperature: 0.3);
        }

        return BuildOfflineDecision(TaskComplexity.Simple);
    }

    // --- Tier 2: Dengeli ---
    private static RoutingDecision RouteStandard(AiOptimizationSettings s, bool hasClaude, bool hasOpenAi, bool hasGemini, bool vision)
    {
        // OpenAI'ın vision'ı bu katman için ideal
        if (hasOpenAi)
        {
            string model = AiModelNormalizer.NormalizeOpenAiTextModel(s.OpenAiModel);
            return new RoutingDecision(
                Provider: "OpenAI",
                ModelId: model.Contains("mini") ? model : "gpt-4o-mini",
                VisionModelId: "gpt-4o",
                Tier: TaskComplexity.Standard,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "conversational",
                MaxOutputTokens: 800,
                Temperature: 0.5);
        }

        if (hasGemini)
        {
            return new RoutingDecision(
                Provider: "Gemini",
                ModelId: "gemini-2.5-flash",
                VisionModelId: "gemini-2.5-flash",
                Tier: TaskComplexity.Standard,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "structured_markdown",
                MaxOutputTokens: 800,
                Temperature: 0.5);
        }

        if (hasClaude)
        {
            return new RoutingDecision(
                Provider: "Claude",
                ModelId: AiModelNormalizer.NormalizeClaudeTextModel(s.ClaudeModel),
                VisionModelId: AiModelNormalizer.NormalizeClaudeTextModel(s.ClaudeModel),
                Tier: TaskComplexity.Standard,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "xml_tags",
                MaxOutputTokens: 800,
                Temperature: 0.5);
        }

        return BuildOfflineDecision(TaskComplexity.Standard);
    }

    // --- Tier 3: Premium / Reasoning ---
    private static RoutingDecision RouteComplex(AiOptimizationSettings s, bool hasClaude, bool hasOpenAi, bool hasGemini, bool vision)
    {
        // Claude Opus kök neden analizi ve stratejik düşünce için en iyi
        if (hasClaude)
        {
            string model = AiModelNormalizer.NormalizeClaudeTextModel(s.ClaudeModel);
            // Opus veya Sonnet kullan (düşük tier modelleri premium'a yükselt)
            if (model.Contains("haiku")) model = "claude-sonnet-5";
            return new RoutingDecision(
                Provider: "Claude",
                ModelId: model,
                VisionModelId: model,
                Tier: TaskComplexity.Complex,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "xml_tags",
                MaxOutputTokens: 2000,
                Temperature: 0.6);
        }

        if (hasOpenAi)
        {
            string model = AiModelNormalizer.NormalizeOpenAiTextModel(s.OpenAiModel);
            // Mini modelleri premium görevler için yükselt
            if (model.Contains("mini")) model = "gpt-4o";
            return new RoutingDecision(
                Provider: "OpenAI",
                ModelId: model,
                VisionModelId: "gpt-4o",
                Tier: TaskComplexity.Complex,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "conversational",
                MaxOutputTokens: 2000,
                Temperature: 0.6);
        }

        if (hasGemini)
        {
            return new RoutingDecision(
                Provider: "Gemini",
                ModelId: "gemini-2.5-pro",
                VisionModelId: "gemini-2.5-pro",
                Tier: TaskComplexity.Complex,
                SupportsVision: true,
                SupportsStructuredOutput: true,
                PromptStyleHint: "structured_markdown",
                MaxOutputTokens: 2000,
                Temperature: 0.6);
        }

        return BuildOfflineDecision(TaskComplexity.Complex);
    }

    private static RoutingDecision BuildOfflineDecision(TaskComplexity tier) => new(
        Provider: "Offline",
        ModelId: "local-heuristic",
        VisionModelId: "local-heuristic",
        Tier: tier,
        SupportsVision: false,
        SupportsStructuredOutput: false,
        PromptStyleHint: "none",
        MaxOutputTokens: 0,
        Temperature: 0.0);
}
