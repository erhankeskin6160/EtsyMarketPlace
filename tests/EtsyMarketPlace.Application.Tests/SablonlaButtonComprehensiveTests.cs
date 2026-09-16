namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class SablonlaButtonComprehensiveTests
{
    private readonly ListingOptimizationService _optimizer = new();

    // =========================================================================
    // TEST 1: "Şablonla butonu çalışıyor mu?"
    // Ham metin, rakip metni veya kısa satıcı notu verildiğinde hatasız çalışıp standart 6-7 bölümlü Etsy açıklaması üretiyor mu?
    // =========================================================================
    [Theory]
    [InlineData("12oz speckled ceramic mug with mountain landscape. Dishwasher safe. Height 9.5cm, diameter 8.5cm.", "Handmade Ceramic Mug 12oz", "ceramic coffee mug")]
    [InlineData("Full grain leather wallet with 8 card slots. Dimensions: 11.5cm x 9cm. Real cowhide.", "Full Grain Leather Wallet", "leather card wallet")]
    [InlineData("Astronaut night light for nursery. USB power, cool touch LED. 18cm x 12cm x 6cm.", "Astronaut LED Night Light", "astronaut night light")]
    public void SablonlaButton_WorksFlawlessly_OnRawUnstructuredInputs(string rawDesc, string title, string keyword)
    {
        // Act: "Şablonla" butonunun çağırdığı fonksiyonu çalıştır
        var formatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            rawDesc,
            title,
            tags: [keyword],
            materials: ["Artisan material"],
            targetKeyword: keyword
        );

        // Assert:
        Assert.NotNull(formatted);
        Assert.True(formatted.Length >= 600, $"Şablonlanmış açıklama çok kısa: {formatted.Length} karakter");
        Assert.Contains("\r\n\r\n", formatted); // Etsy mobil ferah çift satır sonu garantisi

        // En az 4 yapısal bölüm içermeli
        Assert.True(EtsyDescriptionFormatter.IsAlreadyStructuredEtsyDescription(formatted));

        // Ham metindeki teknik ölçüler korunmalı
        if (rawDesc.Contains("9.5cm")) Assert.Contains("9.5cm", formatted);
        if (rawDesc.Contains("11.5cm")) Assert.Contains("11.5cm", formatted);
        if (rawDesc.Contains("18cm")) Assert.Contains("18cm", formatted);
    }

    // =========================================================================
    // TEST 2: "Çalışıyorsa AI açıklamasını bozuyor mu?"
    // AI zengin, özgün ve ürüne özel bir açıklama ürettikten sonra "Şablonla" butonuna basılırsa AI'ın yazdığı metin bozuluyor veya siliniyor mu?
    // =========================================================================
    [Fact]
    public void SablonlaButton_DoesNotCorruptOrOverwrite_AiGeneratedDescription()
    {
        // 1. AI ile zenginleştirilmiş Seramik Kupa açıklaması simülasyonu
        var input = new ListingOptimizationInput(
            Title: "Handmade Speckled Ceramic Coffee Mug 12oz",
            Description: "Artisan stoneware speckled glaze coffee mug. Capacity: 12 oz (350 ml). Height: 9.5 cm. Microwave and dishwasher safe. Handcrafted by ceramic masters.",
            Tags: ["ceramic coffee mug", "handmade mug", "stoneware cup"],
            TargetKeyword: "ceramic coffee mug"
        );

        var aiResult = _optimizer.Optimize(input);
        var originalAiDesc = aiResult.DescriptionDraft;

        // AI açıklamasının zenginliği teyit edilir
        Assert.Contains("12 oz", originalAiDesc);
        Assert.Contains("9.5 cm", originalAiDesc);
        Assert.Contains("dishwasher", originalAiDesc, StringComparison.OrdinalIgnoreCase);

        // 2. Kullanıcı "Şablonla Paragrafla" butonuna basarsa:
        var sablonlanmisDesc = EtsyDescriptionFormatter.FormatToStandardTemplate(
            originalAiDesc,
            aiResult.TitleSuggestions.First(),
            aiResult.TagSuggestions,
            aiResult.MaterialSuggestions,
            input.TargetKeyword
        );

        // Assert: AI açıklaması ASLA silinmemeli veya jenerik metinle ezilmemelidir!
        Assert.NotNull(sablonlanmisDesc);

        // A) AI'ın ürettiği tüm spesifik teknik ve zanaat kelimeleri korunmalı:
        Assert.Contains("12 oz", sablonlanmisDesc);
        Assert.Contains("9.5 cm", sablonlanmisDesc);
        Assert.Contains("dishwasher", sablonlanmisDesc, StringComparison.OrdinalIgnoreCase);

        // B) Karakter kaybı olmamalı (AI açıklamasının %95+ uzunluğu ve içeriği korunmalı):
        var lengthRatio = (double)sablonlanmisDesc.Length / originalAiDesc.Length;
        Assert.True(lengthRatio >= 0.95 && lengthRatio <= 1.05,
            $"Şablonlama AI metnini kırptı veya bozdu! Orijinal: {originalAiDesc.Length}, Şablon sonrası: {sablonlanmisDesc.Length}");

        // C) Satış odaklı cümleler korunmalı:
        Assert.Contains("Brighten your daily rituals", sablonlanmisDesc);
    }

    [Fact]
    public void SablonlaButton_DoesNotCorrupt_LeatherWalletOrAstronautLamp_AiDescriptions()
    {
        // Deri Cüzdan Testi
        var walletInput = new ListingOptimizationInput(
            "Full Grain Leather Bifold Mens Wallet",
            "Full grain cowhide leather wallet. 8 card slots, 1 cash pocket. Measures 11.5cm x 9cm. RFID blocking.",
            ["leather card wallet", "bifold wallet"],
            "leather card wallet"
        );
        var walletAi = _optimizer.Optimize(walletInput);
        var walletFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            walletAi.DescriptionDraft,
            walletAi.TitleSuggestions.First(),
            walletAi.TagSuggestions,
            walletAi.MaterialSuggestions,
            walletInput.TargetKeyword
        );

        Assert.Contains("11.5cm x 9cm", walletFormatted);
        Assert.Contains("8 card slots", walletFormatted);
        Assert.Contains("PREMIUM LEATHER", walletFormatted);
        Assert.Contains("LEATHER CARE", walletFormatted);

        // Astronot Gece Lambası Testi
        var lampInput = new ListingOptimizationInput(
            "Astronaut LED Night Light Nursery Bedside Lamp",
            "Astronaut desk lamp with starry night projection. USB Type-C power. Dimensions 18cm x 12cm. Cool touch LED.",
            ["astronaut night light", "space lamp"],
            "astronaut night light"
        );
        var lampAi = _optimizer.Optimize(lampInput);
        var lampFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            lampAi.DescriptionDraft,
            lampAi.TitleSuggestions.First(),
            lampAi.TagSuggestions,
            lampAi.MaterialSuggestions,
            lampInput.TargetKeyword
        );

        Assert.Contains("18cm x 12cm", lampFormatted);
        Assert.Contains("USB", lampFormatted);
        Assert.Contains("COSMIC GLOW", lampFormatted);
        Assert.Contains("LIGHTING CARE", lampFormatted);
    }

    // =========================================================================
    // TEST 3: "Hep aynı taslakla mı çalışıyor?"
    // Farklı kategorideki ürünler "Şablonla" butonuna basıldığında aynı robotik başlıkları mı alıyor, yoksa nişine özel başlıklar mı alıyor?
    // =========================================================================
    [Fact]
    public void SablonlaButton_DoesNotUseSingleGenericTemplate_ProducesBespokeHeadersPerCategory()
    {
        var mugFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "Ceramic mug 12oz. Microwave safe.",
            "Speckled Ceramic Mug",
            ["ceramic mug"],
            ["ceramic"],
            "ceramic coffee mug"
        );

        var walletFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "Handmade leather wallet with card slots.",
            "Leather Bifold Wallet",
            ["leather wallet"],
            ["leather"],
            "leather card wallet"
        );

        var standFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "Headphone stand for gaming desk setup. 24cm height.",
            "Dragon Headphone Stand",
            ["headphone stand"],
            ["resin"],
            "headphone desk stand"
        );

        var chessFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "Hand carved wooden chess set with board 40cm x 40cm.",
            "Handcrafted Wooden Chess Set",
            ["chess set"],
            ["walnut wood"],
            "carved chess set"
        );

        var dogFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "Custom leather dog collar with brass nameplate. 35cm-45cm.",
            "Personalized Leather Dog Collar",
            ["dog collar"],
            ["leather", "brass"],
            "leather dog collar"
        );

        // 1. Her ürünün kendine has emojili başlıkları olmalı:
        Assert.Contains("☕ ARTISAN CRAFT & DAILY ENJOYMENT", mugFormatted);
        Assert.Contains("📏 CAPACITY, SIZING & CARE", mugFormatted);
        Assert.Contains("🧼 CARE & CLEANING INSTRUCTIONS", mugFormatted);

        Assert.Contains("🐂 PREMIUM LEATHER & TIMELESS CRAFT", walletFormatted);
        Assert.Contains("📏 CARD SLOTS, CAPACITY & MEASUREMENTS", walletFormatted);
        Assert.Contains("🧼 LEATHER CARE & PRESERVATION", walletFormatted);

        Assert.Contains("🎧 BATTLESTATION STYLING & GEAR REST", standFormatted);
        Assert.Contains("📏 MEASUREMENTS & STABILITY DETAILS", standFormatted);
        Assert.Contains("🧼 CARE & DESK MAINTENANCE", standFormatted);

        Assert.Contains("♟️ HAND-CARVED WOODWORK & STRATEGY", chessFormatted);
        Assert.Contains("📏 BOARD & PIECE MEASUREMENTS", chessFormatted);
        Assert.Contains("🧼 WOOD CARE & HEIRLOOM PRESERVATION", chessFormatted);

        Assert.Contains("🐾 PET COMFORT & DURABLE HARDWARE", dogFormatted);
        Assert.Contains("📏 SIZING & COLLAR ADJUSTMENT", dogFormatted);
        Assert.Contains("🧼 COLLAR CARE & LONGEVITY", dogFormatted);

        // 2. Birbirlerinin niş başlıklarını asla taşımamalılar (Kalıp olmadığını kanıtlar):
        Assert.DoesNotContain("☕ ARTISAN CRAFT", walletFormatted);
        Assert.DoesNotContain("🐂 PREMIUM LEATHER", mugFormatted);
        Assert.DoesNotContain("🎧 BATTLESTATION", chessFormatted);
        Assert.DoesNotContain("♟️ HAND-CARVED", dogFormatted);
        Assert.DoesNotContain("🐾 PET COMFORT", standFormatted);
    }

    // =========================================================================
    // TEST 4: İki veya Üç Kez Basılınca Bozulma / Çift Emoji Oluyor mu? (Idempotency Test)
    // =========================================================================
    [Fact]
    public void SablonlaButton_IsIdempotent_MultipleClicksProduceIdenticalOutput_NoDuplicateEmojis()
    {
        var initialRaw = "Handmade speckled ceramic coffee mug 12oz. Microwave safe. Height 9.5cm.";

        // 1. Tık: İlk formatlama
        var click1 = EtsyDescriptionFormatter.FormatToStandardTemplate(
            initialRaw,
            "Handmade Ceramic Mug 12oz",
            ["ceramic mug"],
            ["ceramic"],
            "ceramic coffee mug"
        );

        // 2. Tık: Kullanıcı butona tekrar bastı
        var click2 = EtsyDescriptionFormatter.FormatToStandardTemplate(
            click1,
            "Handmade Ceramic Mug 12oz",
            ["ceramic mug"],
            ["ceramic"],
            "ceramic coffee mug"
        );

        // 3. Tık: Kullanıcı butona 3. kez bastı
        var click3 = EtsyDescriptionFormatter.FormatToStandardTemplate(
            click2,
            "Handmade Ceramic Mug 12oz",
            ["ceramic mug"],
            ["ceramic"],
            "ceramic coffee mug"
        );

        // Assert: Çıktılar harfi harfine aynı olmalı (Idempotent), asla bozulmamalı!
        Assert.Equal(click1, click2);
        Assert.Equal(click2, click3);

        // Çift emoji veya çift madde işareti oluşmamalı:
        Assert.DoesNotContain("☕ ☕", click3);
        Assert.DoesNotContain("📏 📏", click3);
        Assert.DoesNotContain("✨ ✨", click3);
        Assert.DoesNotContain("• •", click3);
    }
}
