namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed record PromptAnalysis(
    int Score,
    List<string> Strengths,
    List<string> Warnings,
    List<string> Tips,
    string EnhancedPrompt);

internal sealed record BackgroundPreset(
    string Name,
    string Icon,
    string Category,
    string Prompt,
    string Vibe = "");

/// <summary>
/// Etsy ürün fotoğrafçılığı için akıllı prompt analizi, puanlama, ipuçları ve kategori şablonları servisi.
/// </summary>
internal static class PromptTipsService
{
    public static readonly List<BackgroundPreset> Presets =
    [
        // Ev & Yaşam (Cozy Living)
        new("Sıcak İskandinav", "🏠", "Ev & Yaşam", "Warm cozy Scandinavian living room, natural sunlight streaming through sheer curtains, soft blurred bokeh background, neutral beige tones", "Aydınlık & Sıcak"),
        new("Kahve & Kafe Köşesi", "☕", "Ev & Yaşam", "Aesthetic modern coffee shop corner, rustic reclaimed wood table, soft steam, warm morning sunlight, cozy atmosphere", "Sıcak & Samimi"),
        new("Şömine & Romantik", "🕯️", "Ev & Yaşam", "Cozy evening beside a glowing fireplace, rustic brick hearth, warm candlelight reflections, soft shadows, intimate ambiance", "Akşam Sıcaklığı"),
        new("Bohem Oturma Odası", "🪴", "Ev & Yaşam", "Sunny bohemian living room, macrame wall art softly blurred in background, rattan furniture, natural earthy textures, warm ambient glow", "Bohem & Sıcak"),

        // Doğal & Organik (Botanical)
        new("Doğal Botanik", "🌿", "Doğal & Botanik", "Lush green botanical garden setting, fresh eucalyptus leaves and soft monstera shadows, soft dappled morning sunlight, organic earth tones", "Taze Okaliptüs"),
        new("Güneşli Sera", "🌱", "Doğal & Botanik", "Sunlit greenhouse interior, exotic tropical leaves, glass roof with warm sunlight filtering through, fresh organic atmosphere", "Organik Sera"),
        new("Kuru Çiçek & Pampa", "🌾", "Doğal & Botanik", "Delicate dried pampas grass and bunny tails, warm terracotta backdrop, soft natural shadows, minimalist boho aesthetic", "Boho & Pampa"),
        new("Bambu & Zen Bahçe", "🍃", "Doğal & Botanik", "Peaceful Japanese zen garden, smooth river stones, subtle green bamboo accents, tranquil soft diffused lighting", "Zen & Huzur"),

        // Lüks & Takı (Luxury)
        new("Carrara Mermer", "🏛️", "Lüks & Takı", "Polished white Carrara marble counter, soft neutral diffused museum lighting, ultra-clean minimalist luxury aesthetic, elegant reflections", "Saf Beyaz Mermer"),
        new("Siyah Kadife Vitrin", "💎", "Lüks & Takı", "Premium matte black velvet surface, dramatic moody jewelry spotlighting, high contrast luxury aesthetic, crisp fine highlights", "Dramatik Lüks"),
        new("Minimalist Podyum", "✨", "Lüks & Takı", "Architectural geometric pedestal podium in warm cream beige, subtle elegant cast shadows, high-end editorial product photoshoot", "Modern Podyum"),
        new("Yansımalı Akrilik", "🪞", "Lüks & Takı", "Sleek glossy white acrylic surface with crisp mirror-like reflection, commercial strobe lighting, ultra clean modern elegance", "Kristal Yansıma"),

        // Artisan & Rustik (Rustic Wood)
        new("Artisan Ahşap", "🪵", "Artisan & Rustik", "Weathered rustic oak wooden surface, warm morning window side lighting, artisan workshop ambiance, rich natural wood grain", "Ham Meşe"),
        new("Ham Keten Dokulu", "🧵", "Artisan & Rustik", "Textured natural unbleached raw linen fabric, gentle organic folds, soft neutral daylight, handcrafted artisan product photography", "Doğal Keten"),
        new("Endüstriyel Tuğla", "🧱", "Artisan & Rustik", "Exposed vintage red brick wall, dark stained wooden workbench, warm loft lighting, authentic industrial artisan workshop", "Loft & Tuğla"),
        new("Çömlek & Terracotta", "🏺", "Artisan & Rustik", "Warm rustic terracotta tile surface, Mediterranean ceramic studio, sunbeams casting soft shadows, artisanal craft vibe", "Terracotta"),

        // E-Ticaret & Stüdyo (Commercial)
        new("E-Ticaret Beyaz", "📦", "Stüdyo & Katalog", "Pure infinite seamless studio white background, commercial e-commerce catalog photography, balanced dual softbox lighting, soft contact shadow", "Sonsuz Beyaz"),
        new("Nötr Açık Gri Stüdyo", "🏢", "Stüdyo & Katalog", "Professional neutral light gray studio cyclorama, balanced soft lighting, subtle gradient backdrop, crisp clean e-commerce standard", "Nötr Gri Stüdyo"),
        new("Softbox Vitrin", "💡", "Stüdyo & Katalog", "Modern studio lightbox setup, continuous soft illumination, zero harsh reflections, perfect true-to-life color rendering", "Yumuşak Işık"),

        // Trend & Mevsimsel (Aesthetic & Seasonal)
        new("Pastel Gradyan", "🎨", "Trend & Mevsim", "Soft pastel gradient background blending warm peach and lavender, dreamy ethereal studio lighting, clean contemporary product display", "Peach & Lavender"),
        new("Altın Saat (Sunset)", "🌅", "Trend & Mevsim", "Outdoor wooden deck during golden hour sunset, warm magical backlight, glowing rim light, natural lifestyle product ambiance", "Büyülü Gün Batımı"),
        new("Sahil & Kumsal", "🏖️", "Trend & Mevsim", "Fine white beach sand surface, gentle turquoise ocean waves softly blurred in background, natural driftwood accent, bright coastal sunlight", "Deniz & Kumsal"),
        new("Kış & Yılbaşı", "🎄", "Trend & Mevsim", "Festive cozy holiday scene, subtle warm fairy bokeh lights in background, frosted pine cone accents, warm golden ambient glow", "Yılbaşı Işıltısı"),
        new("Yaz Terası", "☀️", "Trend & Mevsim", "Sun-drenched Mediterranean outdoor terracotta terrace, bright summer daylight, subtle palm leaf shadows, cheerful vibrant mood", "Yaz Enerjisi")
    ];

    /// <summary>
    /// Verilen promptu Etsy standartlarına göre 0-100 puanlayıp güçlü yanları, uyarıları ve iyileştirilmiş versiyonunu döner.
    /// </summary>
    public static PromptAnalysis AnalyzePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new PromptAnalysis(
                0,
                [],
                ["⚠️ Prompt henüz boş. Ürünün duracağı zemin veya ortamı tarif edin."],
                ["💡 Örnek: 'rustic wooden table with warm morning sunlight'"],
                "Commercial product on a clean rustic wooden table with warm natural morning window light, soft blurred background");
        }

        string p = prompt.ToLowerInvariant();
        int score = 40; // Temel puan
        var strengths = new List<string>();
        var warnings = new List<string>();
        var tips = new List<string>();

        // 1. Zemin / Yüzey Analizi (20 puan)
        string[] surfaces = ["wood", "wooden", "marble", "table", "surface", "counter", "shelf", "sand", "concrete", "stone", "linen", "cloth", "ahşap", "mermer", "masa", "zemin"];
        if (surfaces.Any(s => p.Contains(s)))
        {
            score += 20;
            strengths.Add("✅ Ürün zemini/yüzeyi başarıyla belirtilmiş.");
        }
        else
        {
            warnings.Add("⚠️ Ürünün üzerinde duracağı zemin (örn: 'wooden table', 'marble counter') belirtilmemiş.");
            tips.Add("💡 Zemin ekleyin: 'on a rustic wooden tabletop' veya 'on polished white marble'");
        }

        // 2. Aydınlatma Analizi (20 puan)
        string[] lightings = ["sunlight", "light", "lighting", "glow", "softbox", "ambient", "golden hour", "diffused", "bright", "dappled", "ışık", "aydınlatma", "güneş"];
        if (lightings.Any(s => p.Contains(s)))
        {
            score += 20;
            strengths.Add("✅ Işık ve aydınlatma atmosferi tarif edilmiş.");
        }
        else
        {
            warnings.Add("⚠️ Işıklandırma türü eksik. Işık ürünün doğallığını belirler.");
            tips.Add("💡 Işık ekleyin: 'warm morning window light' veya 'soft diffused daylight'");
        }

        // 3. Kompozisyon & Odak / Bokeh (10 puan)
        string[] focus = ["bokeh", "blur", "blurred", "depth of field", "shallow", "cinematic", "focus", "odak", "arka plan"];
        if (focus.Any(s => p.Contains(s)))
        {
            score += 10;
            strengths.Add("✅ Odak ve derinlik (bokeh / depth of field) kurgulanmış.");
        }
        else
        {
            tips.Add("💡 Ürünü öne çıkarmak için arka plan bulanıklığı ekleyin: 'shallow depth of field, softly blurred background'");
        }

        // 4. Gerçekçilik & Gölge (10 puan)
        string[] shadows = ["shadow", "shadows", "realistic", "natural", "commercial", "photorealistic", "gölge", "doğal"];
        if (shadows.Any(s => p.Contains(s)))
        {
            score += 10;
            strengths.Add("✅ Gölge ve gerçekçilik parametreleri eklenmiş.");
        }
        else
        {
            tips.Add("💡 Ürünün havada uçuyor gibi görünmemesi için: 'natural contact shadow'");
        }

        // Uzunluk kontrolü
        if (prompt.Length < 25)
        {
            warnings.Add("⚠️ Prompt çok kısa. Detay eklemek AI'ın rastgele üretmesini engeller.");
        }

        score = Math.Clamp(score, 10, 100);

        string enhanced = EnhancePromptLocally(prompt);

        return new PromptAnalysis(score, strengths, warnings, tips, enhanced);
    }

    /// <summary>
    /// Kullanıcının promptunu profesyonel Etsy e-ticaret fotoğrafçılığı kalıplarıyla zenginleştirir.
    /// </summary>
    public static string EnhancePromptLocally(string rawPrompt, string? productTitle = null)
    {
        string p = rawPrompt.Trim();
        if (string.IsNullOrWhiteSpace(p))
        {
            p = "elegant product scene";
        }

        // Eğer eksikse tamamlayıcı unsurları ekle
        var additions = new List<string>();
        string lower = p.ToLowerInvariant();

        if (!lower.Contains("light") && !lower.Contains("sun"))
        {
            additions.Add("soft natural window daylight");
        }

        if (!lower.Contains("blur") && !lower.Contains("bokeh") && !lower.Contains("depth"))
        {
            additions.Add("shallow depth of field with softly blurred background");
        }

        if (!lower.Contains("shadow"))
        {
            additions.Add("delicate realistic contact shadow");
        }

        if (!lower.Contains("commercial") && !lower.Contains("photorealistic"))
        {
            additions.Add("commercial high-end Etsy marketplace product photography, 8k resolution, photorealistic");
        }

        string result = p;
        if (additions.Count > 0)
        {
            result += ", " + string.Join(", ", additions);
        }

        return result;
    }

    /// <summary>
    /// Etsy kategorisine ve mevsime göre en çok satan arka plan stilini önerir.
    /// </summary>
    public static string GetCategoryRecommendation(string category, string? season = null)
    {
        string cat = category.ToLowerInvariant();

        if (cat.Contains("jewelry") || cat.Contains("taki") || cat.Contains("yuzuk") || cat.Contains("kolye"))
        {
            return "Polished white marble tray, velvet ring box accent, soft warm museum spotlight, delicate shadows, macro focus";
        }
        if (cat.Contains("candle") || cat.Contains("mum") || cat.Contains("home") || cat.Contains("ev"))
        {
            return "Cozy living room coffee table with open linen magazine, soft morning sunlight, neutral beige tones, blurred fireplace background";
        }
        if (cat.Contains("clothing") || cat.Contains("giyim") || cat.Contains("tshirt") || cat.Contains("elbise"))
        {
            return "Minimalist boutique clothing rack, light oak hardwood floor, warm interior studio lighting, clean architectural background";
        }
        if (cat.Contains("ceramic") || cat.Contains("pottery") || cat.Contains("mug") || cat.Contains("fincan") || cat.Contains("kupa"))
        {
            return "Sunlit modern kitchen counter, warm ceramic tiles, fresh espresso beans nearby, soft morning golden hour glow";
        }
        if (cat.Contains("leather") || cat.Contains("deri") || cat.Contains("cuzdan") || cat.Contains("canta"))
        {
            return "Dark walnut artisan workbench, brass crafting tools softly blurred in background, moody warm directional side lighting";
        }

        return "Clean minimalist wooden tabletop, soft natural window light, subtle indoor green plant in blurred background, professional catalog look";
    }
}
