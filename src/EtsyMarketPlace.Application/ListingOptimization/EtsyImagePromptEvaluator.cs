namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed record PromptEvaluationResult(
    int Score,
    List<string> Strengths,
    List<string> Warnings,
    List<string> Tips,
    string EnhancedPrompt);

/// <summary>
/// Etsy ürün fotoğrafçılığı ve AI sahne promptları için kurallara dayalı kalite değerlendirme ve iyileştirme motoru.
/// </summary>
public static class EtsyImagePromptEvaluator
{
    private static readonly string[] SurfaceKeywords =
    [
        "wood", "wooden", "marble", "table", "surface", "counter", "shelf", "sand", "concrete", "stone", "linen", "cloth",
        "ahşap", "mermer", "masa", "zemin", "sehpa", "tepsi"
    ];

    private static readonly string[] LightingKeywords =
    [
        "sunlight", "light", "lighting", "glow", "softbox", "ambient", "golden hour", "diffused", "bright", "dappled", "shadow",
        "ışık", "aydınlatma", "güneş", "gölge"
    ];

    private static readonly string[] FocusKeywords =
    [
        "bokeh", "blur", "blurred", "depth of field", "shallow", "cinematic", "focus",
        "odak", "derinlik", "arka plan"
    ];

    /// <summary>
    /// Verilen promptu 0-100 arasında puanlar ve eksik/güçlü yönleri listeler.
    /// </summary>
    public static PromptEvaluationResult Evaluate(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new PromptEvaluationResult(
                Score: 0,
                Strengths: [],
                Warnings: ["⚠️ Prompt boş bırakılamaz. Ürünün sergileneceği zemin ve ışığı belirtin."],
                Tips: ["💡 Örnek: 'on a rustic oak table with warm morning sunlight'"],
                EnhancedPrompt: "Commercial product on a clean rustic wooden table with warm natural morning window light, soft blurred background");
        }

        string p = prompt.Trim().ToLowerInvariant();
        int score = 40;
        var strengths = new List<string>();
        var warnings = new List<string>();
        var tips = new List<string>();

        // 1. Zemin ve Yüzey Analizi (25 puan)
        if (SurfaceKeywords.Any(s => p.Contains(s)))
        {
            score += 25;
            strengths.Add("✅ Zemin / Yüzey belirtilmiş.");
        }
        else
        {
            warnings.Add("⚠️ Ürünün duracağı zemin belirtilmemiş.");
            tips.Add("💡 Zemin ekleyin (örn: 'rustic wooden table' veya 'white marble counter').");
        }

        // 2. Aydınlatma ve Işık Analizi (25 puan)
        if (LightingKeywords.Any(s => p.Contains(s)))
        {
            score += 25;
            strengths.Add("✅ Aydınlatma türü belirtilmiş.");
        }
        else
        {
            warnings.Add("⚠️ Işıklandırma türü eksik.");
            tips.Add("💡 Işık ekleyin (örn: 'warm morning sunlight' veya 'soft diffused studio light').");
        }

        // 3. Derinlik ve Odak (10 puan)
        if (FocusKeywords.Any(s => p.Contains(s)))
        {
            score += 10;
            strengths.Add("✅ Odak ve derinlik (bokeh) unsuru mevcut.");
        }
        else
        {
            tips.Add("💡 Arka plan bulanıklığı ekleyin: 'shallow depth of field, soft blur'.");
        }

        score = Math.Clamp(score, 10, 100);
        string enhanced = Enhance(prompt);

        return new PromptEvaluationResult(score, strengths, warnings, tips, enhanced);
    }

    /// <summary>
    /// Ham promptu Etsy e-ticaret standartlarına göre profesyonelleştirir.
    /// </summary>
    public static string Enhance(string rawPrompt)
    {
        string p = rawPrompt.Trim();
        if (string.IsNullOrWhiteSpace(p)) p = "handcrafted artisan product";

        var additions = new List<string>();
        string lower = p.ToLowerInvariant();

        if (!SurfaceKeywords.Any(s => lower.Contains(s)))
        {
            additions.Add("on a clean natural wooden surface");
        }

        if (!LightingKeywords.Any(s => lower.Contains(s)))
        {
            additions.Add("soft natural window daylight with delicate contact shadow");
        }

        if (!FocusKeywords.Any(s => lower.Contains(s)))
        {
            additions.Add("shallow depth of field with softly blurred background");
        }

        if (!lower.Contains("commercial") && !lower.Contains("8k") && !lower.Contains("photorealistic"))
        {
            additions.Add("commercial high-end Etsy product photography, 8k resolution, photorealistic");
        }

        return additions.Count > 0 ? $"{p}, {string.Join(", ", additions)}" : p;
    }
}
