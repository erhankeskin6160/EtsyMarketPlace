namespace SimilarProductsWinForms.Studio.Core;

public static class StudioPresets
{
    public static readonly string[] LightingOptions =
    [
        "☀️ Doğal Sabah Işığı (Natural Morning Sunlight)",
        "🌅 Altın Saat Sıcaklığı (Golden Hour Cinematic)",
        "💡 Ticari Softbox Stüdyo (Commercial High-Key)",
        "🕯️ Sıcak Mum & Ahşap Loşluğu (Warm Ambient Cozy)",
        "✨ Minimalist Dağınık Gün Işığı (Diffused Daylight)",
        "🌑 Dramatik Yan Işık (Moody Chiaroscuro)"
    ];

    public static readonly string[] CameraAngleOptions =
    [
        "👀 Göz Hizası (50mm Commercial Eye-Level)",
        "📐 Üstten Kuşbakışı (Flat Lay 90° Top-Down)",
        "🏆 45° Kahraman Açısı (Hero 45° Perspective)",
        "🔍 Makro Doku Detayı (Macro Close-Up)",
        "🛋️ Geniş Yaşam Alanı (Wide Environmental Context)"
    ];

    public static readonly string[] ScenePresets =
    [
        "🏛️ Lüks İtalyan Mermeri & Sabah Güneşi",
        "🪵 Rustik Masif Meşe Masa & Keten Örtü",
        "🌿 Botanik Bahçe & Bohem Gün Işığı",
        "⚪ Saf Beyaz Ticari Stüdyo & Yumuşak Temas Gölgesi",
        "🛁 Lüks Spa / Banyo Tezgahı & Bambu Detaylar",
        "☕ Paris Kafe Balkonu & Yumuşak Arka Plan Bulanıklığı (Bokeh)"
    ];

    public static string BuildCompositePrompt(string basePrompt, string lighting, string cameraAngle, string scenePreset)
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(basePrompt))
        {
            sb.Append(basePrompt.Trim());
        }

        if (!string.IsNullOrWhiteSpace(scenePreset) && !scenePreset.StartsWith("Seçiniz"))
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append("Scene: ").Append(scenePreset);
        }

        if (!string.IsNullOrWhiteSpace(lighting))
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append("Lighting: ").Append(lighting);
        }

        if (!string.IsNullOrWhiteSpace(cameraAngle))
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append("Camera: ").Append(cameraAngle);
        }

        if (sb.Length > 0) sb.Append(", ");
        sb.Append("ultra-realistic commercial product photography, 8k resolution, crisp focus, hyper-detailed texture, depth of field");

        return sb.ToString();
    }
}
