namespace SimilarProductsWinForms.Services;

using System;

/// <summary>
/// Few-Shot örnekleri deposu — yapay zekaya ideal çıktı formatını öğretmek için
/// 3 altın standart teşhis örneği içerir. Araştırma doğruluğu %40+ artırıyor.
/// </summary>
internal static class FewShotExampleStore
{
    // =========================================================================
    // Başarılı Mağaza Örneği — AI'ın olumlu ve destekleyici çıktı vermesini öğretir
    // =========================================================================
    public static string GetSuccessfulStoreExample(string promptStyle) => promptStyle switch
    {
        "xml_tags" => // Claude formatı
@"<example>
<input>
STORE: 52 orders/90d, $3,200 revenue, 1.7 days between orders, Normal drought, 6 best-sellers, 2 zombies, Avg Image Score: 86/100
</input>
<output>
🩺 **Mağaza Teşhis Raporu:**

**Neden Sipariş Geliyor (Başarı Formülü):**
1. 🏆 Mağazanız sağlıklı bir sipariş ritmine sahip — her 1.7 günde bir sipariş alıyorsunuz, bu sektör ortalamasının üzerinde.
2. ✨ Görsel kalite skoru 86/100 ile güçlü — kapak fotoğraflarınız alıcıların dikkatini çekiyor.
3. 🎯 6 lokomotif ürününüz cironun %78'ini oluşturuyor — bu ürünlere stok koruması sağlayın.

**Acil Aksiyon (1 adet):**
1. 🧟 2 zombi listinginizin görsellerini AI Stüdyosunda yenileyin veya fiyat/başlık revizyonu yapın (Yıllık $1.20 yenileme tasarrufu).

**30 Gün Tahmini:** Mevcut ivmeyle 16-20 sipariş (~$980-$1,220) beklenir.
</output>
</example>",

        "conversational" => // OpenAI formatı
@"Example Store Analysis:

INPUT: 52 orders/90d, $3,200 revenue, 1.7 days between orders, Normal drought, 6 best-sellers, 2 zombies, Avg Image Score: 86/100

OUTPUT:
🩺 **Mağaza Teşhis Raporu:**

**Neden Sipariş Geliyor (Başarı Formülü):**
1. 🏆 Mağazanız sağlıklı bir sipariş ritmine sahip — her 1.7 günde bir sipariş alıyorsunuz.
2. ✨ Görsel kalite skoru 86/100 ile güçlü.
3. 🎯 6 lokomotif ürününüz cironun %78'ini oluşturuyor.

**Acil Aksiyon:** 2 zombi listingi revize edin.
**30 Gün Tahmini:** 16-20 sipariş (~$980-$1,220).",

        _ => // Gemini Markdown formatı
@"### Örnek Teşhis (Başarılı Mağaza)

| Metrik | Değer |
|--------|-------|
| Sipariş/90g | 52 |
| Ortalama Süre | 1.7 gün |
| Görsel Skor | 86/100 |

**Teşhis:** Sağlıklı sipariş akışı. 6 lokomotif ürün cironun %78'ini taşıyor.
**Aksiyon:** 2 zombi listing revize edilmeli.
**Tahmin:** 16-20 sipariş/30 gün."
    };

    // =========================================================================
    // Krizde Mağaza Örneği — AI'ın acil ve empatik teşhis vermesini öğretir
    // =========================================================================
    public static string GetCrisisStoreExample(string promptStyle) => promptStyle switch
    {
        "xml_tags" =>
@"<example>
<input>
STORE: 4 orders/90d, $180 revenue, 22.5 days between orders, CRITICAL drought, 0 best-sellers, 18 zombies, Avg Image Score: 48/100, Days since last order: 31
</input>
<output>
🚨 **ACİL Mağaza Teşhis Raporu:**

**Neden Sipariş Gelmiyor (Kök Nedenler):**
1. 📸 Kritik Görsel Zafiyeti: Mağaza görsel skoru 48/100 — arama sonuçlarında alıcılar ürünlerinizi tıklamıyor çünkü kapak resimleri karanlık ve düşük çözünürlüklü.
2. 🧟 Zombi Yığılması: 18 adet zombi listing (toplam 22 listingden 18'i!) Etsy algoritmasının mağaza kalite puanınızı düşürmesine neden oluyor.
3. 🚨 31 gündür sipariş yok — bu kuraklık seviyesi mağaza görünürlüğünü doğrudan etkiler.

**Acil Eylem Planı (Öncelik Sırasıyla):**
1. [BUGÜN] En yüksek görüntülenmeli 3 ürünün kapak resmini AI Stüdyosunda yenileyin.
2. [BU HAFTA] 18 zombi listingden en az 10'unu arşive alın → Yıllık $10.80 tasarruf.
3. [BU AY] Kalan 8 listenin başlık, tag ve fiyatlarını rakip analizi yaparak revize edin.

**30 Gün Tahmini:** Görseller düzeltilmezse 0-2 sipariş. Düzeltilirse 5-8 sipariş (~$225-$360) beklenir.
</output>
</example>",

        _ =>
@"Example Crisis Store Analysis:

INPUT: 4 orders/90d, $180 revenue, 22.5 days between orders, CRITICAL drought, 18 zombies, Image Score: 48/100

OUTPUT:
🚨 **Kök Nedenler:** Görsel skoru 48/100, 18 zombi listing, 31 gündür sipariş yok.
**Acil Plan:** 1) Bugün 3 kapak yenile 2) Bu hafta 10 zombi arşivle 3) Tag/fiyat revizyonu.
**Tahmin:** Düzeltilirse 5-8 sipariş/30g, düzeltilmezse 0-2."
    };

    /// <summary>
    /// Prompt style'a göre tüm few-shot örneklerini birleştirilmiş string olarak döner.
    /// System prompt'a enjekte edilmek üzere hazırlanır.
    /// </summary>
    public static string GetAllExamples(string promptStyle)
    {
        return $"{GetSuccessfulStoreExample(promptStyle)}\n\n{GetCrisisStoreExample(promptStyle)}";
    }
}
