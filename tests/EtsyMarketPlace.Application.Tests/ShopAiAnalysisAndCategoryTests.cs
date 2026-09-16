namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class ShopAiAnalysisAndCategoryTests
{
    // =========================================================================
    // 1. TEST: "Hızlı Ürün Ekle" - Kategori Tespiti Her Üründe Farklı mı?
    // "hızlı ürün eklede hep aynı kategoriyimi buluyor bunları test et"
    // =========================================================================
    [Theory]
    [InlineData("Astronaut LED Night Light - 3D Printed Bedside Desk Lamp for Kids Room", 1042, "Night Lights")]
    [InlineData("Handmade Ceramic Coffee Mug with Mountain Glaze - 12oz Tea Cup", 943, "Mugs")]
    [InlineData("Full Grain Leather Wallet - Slim Bifold Card Holder for Men", 142, "Wallets & Money Clips")]
    [InlineData("3D Printed Dragon Headphone Stand - Gaming Headset Holder", 2079, "Headphone & Headset Stands")]
    [InlineData("Geometric Wood Wall Art - Modern Mountain Hanging Sign Panel", 1054, "Wall Decor")]
    [InlineData("Handmade Concrete Tealight Candle Holder - Nordic Candle Stand", 1063, "Candleholders")]
    [InlineData("Gollum 3D Printed Statue Bust | Lord of the Rings Figurine", 1239, "Sculptures")]
    [InlineData("Gold Plated Dainty Choker Necklace - Minimalist Pendant", 204, "Necklaces")]
    public void FastListingCreator_DetectsDiverseCategories_Accurately(string title, long expectedTaxonomyId, string expectedCategoryKeyword)
    {
        // Act: Kategori sezgisel analizini çalıştır
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        // Assert: Her ürün kendi doğru kategorisine atanmalı; ASLA hepsi 1239 (Bust/Sculpture) olmamalı!
        Assert.NotNull(result);
        Assert.Equal(expectedTaxonomyId, result.TaxonomyId);
        Assert.Contains(expectedCategoryKeyword, result.CategoryPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.ConfidenceScore >= 70, $"Güven skoru 70 üzerinde olmalı, gelen: {result.ConfidenceScore}");
    }

    [Fact]
    public void FastListingCreator_SevenDiverseProducts_ProduceDistinctCategories()
    {
        var testProducts = new[]
        {
            "Astronaut LED Night Light Bedside Desk Lamp",
            "Handmade Ceramic Coffee Mug 12oz",
            "Full Grain Leather Bifold Wallet Card Holder",
            "Dragon Headphone Stand Gaming Audio Holder",
            "Geometric Wood Wall Art Panel Decor",
            "Handmade Concrete Tealight Candle Holder",
            "Gollum 3D Printed Statue Bust Sculpture"
        };

        var taxonomyIds = testProducts
            .Select(p => LocalCategoryHeuristics.SuggestFromText(p).TaxonomyId)
            .ToList();

        // 7 farklı ürün için en az 7 benzersiz kategori taxonomy ID üretilmeli!
        var distinctCount = taxonomyIds.Distinct().Count();
        Assert.Equal(7, distinctCount);
    }

    // =========================================================================
    // 2. TEST: Mağaza AI Analizi - Ürün neyse ona göre mi üretiyor?
    // "yapay zeka nasıl çalışıyor hep aynı metnimi üretiyor aynı tagmi oluşturuyor"
    // =========================================================================
    [Fact]
    public void ListingOptimization_AstronautLamp_GeneratesBespokeSpaceMetadata_NoGamingBoilerplate()
    {
        var optimizer = new ListingOptimizationService();
        var input = new ListingOptimizationInput(
            Title: "Astronaut Lamp",
            Description: "Astronaut LED night light with 16 color modes and USB cable. Height is 15cm. Perfect bedside glow.",
            Tags: ["astronaut", "lamp", "space", "gift", "decor"],
            TargetKeyword: "astronaut night light"
        );

        var result = optimizer.Optimize(input);

        // 1. Başlık Kontrolleri
        Assert.NotEmpty(result.TitleSuggestions);
        var title = result.TitleSuggestions[0];
        Assert.InRange(title.Length, 70, 140);
        Assert.DoesNotContain("|", title); // Boru karakteri kaldırıldı
        Assert.Contains("Astronaut", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gamer", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anime", title, StringComparison.OrdinalIgnoreCase);

        // 2. Tag Kontrolleri (13 adet, 2-3 kelime, kök kelime tekrar sınırı)
        Assert.Equal(13, result.TagSuggestions.Count);
        foreach (var tag in result.TagSuggestions)
        {
            Assert.True(tag.Length <= 20, $"Tag 20 karakterden uzun olamaz: '{tag}' ({tag.Length})");
            Assert.True(tag.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2, $"Tag en az 2 kelimeden oluşmalı: '{tag}'");
        }

        // Kök kelime frekans kontrolü: 'astronaut' ve 'space' en fazla 2-3 kez geçmeli
        var allTagWords = result.TagSuggestions.SelectMany(t => t.Split(' ')).ToList();
        var astronautCount = allTagWords.Count(w => w.Equals("astronaut", StringComparison.OrdinalIgnoreCase));
        var spaceCount = allTagWords.Count(w => w.Equals("space", StringComparison.OrdinalIgnoreCase));
        Assert.True(astronautCount <= 3, $"'astronaut' kelimesi taglerde en fazla 3 kez geçmeli, gelen: {astronautCount}");
        Assert.True(spaceCount <= 3, $"'space' kelimesi taglerde en fazla 3 kez geçmeli, gelen: {spaceCount}");

        // 3. Açıklama Kontrolleri
        var desc = result.DescriptionDraft;
        Assert.Contains("Astronaut", desc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cosmic", desc, StringComparison.OrdinalIgnoreCase);
        // ASLA alakasız oyun/anime klişesi içermemeli!
        Assert.DoesNotContain("Gamers, anime lovers", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cosplay", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListingOptimization_CeramicMug_GeneratesKitchenSpecificMetadata()
    {
        var optimizer = new ListingOptimizationService();
        var input = new ListingOptimizationInput(
            Title: "Ceramic Mug",
            Description: "12oz ceramic coffee mug with hand-thrown speckled clay and mountain glaze. Dishwasher and microwave safe. Height 10cm.",
            Tags: ["mug", "cup", "coffee", "handmade"],
            TargetKeyword: "ceramic coffee mug"
        );

        var result = optimizer.Optimize(input);

        // 1. Başlık
        var title = result.TitleSuggestions[0];
        Assert.Contains("Mug", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("astronaut", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gamer", title, StringComparison.OrdinalIgnoreCase);

        // 2. Tagler
        Assert.Equal(13, result.TagSuggestions.Count);
        Assert.Contains(result.TagSuggestions, t => t.Contains("mug", StringComparison.OrdinalIgnoreCase) || t.Contains("coffee", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.TagSuggestions, t => t.Contains("astronaut", StringComparison.OrdinalIgnoreCase));

        // 3. Açıklama
        var desc = result.DescriptionDraft;
        Assert.Contains("coffee", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gamers, anime lovers", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("astronaut", desc, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 3. TEST: Farklı Ürünler Arasında Tag ve Metin Çeşitlilik Karşılaştırması
    // "hep aynı metnimi üretiyor aynı tagmi oluşturuyor"
    // =========================================================================
    [Fact]
    public void ListingOptimization_DiverseProducts_ProduceZeroTagEchoAndLowOverlap()
    {
        var optimizer = new ListingOptimizationService();

        var astronaut = optimizer.Optimize(new ListingOptimizationInput(
            "Astronaut LED Night Light", "Space lamp for nursery.", ["astronaut", "lamp", "space"], "astronaut night light"));

        var mug = optimizer.Optimize(new ListingOptimizationInput(
            "Ceramic Mountain Coffee Mug", "Handmade pottery cup 12oz.", ["mug", "coffee", "cup"], "ceramic coffee mug"));

        var wallet = optimizer.Optimize(new ListingOptimizationInput(
            "Leather Bifold Card Wallet", "Full grain leather cardholder.", ["wallet", "leather", "card"], "leather bifold wallet"));

        // Tag setleri
        var astronautTags = astronaut.TagSuggestions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mugTags = mug.TagSuggestions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var walletTags = wallet.TagSuggestions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Astronaut ile Kupa arasındaki tag kesişimi 0 olmalı!
        var astroMugOverlap = astronautTags.Intersect(mugTags).Count();
        Assert.Equal(0, astroMugOverlap);

        // Kupa ile Cüzdan arasındaki tag kesişimi 0 olmalı!
        var mugWalletOverlap = mugTags.Intersect(walletTags).Count();
        Assert.Equal(0, mugWalletOverlap);

        // Astronaut ile Cüzdan arasındaki tag kesişimi 0 olmalı!
        var astroWalletOverlap = astronautTags.Intersect(walletTags).Count();
        Assert.Equal(0, astroWalletOverlap);

        // Açıklamaların ilk cümleleri tamamen farklı olmalı
        Assert.NotEqual(astronaut.DescriptionDraft.Substring(0, 50), mug.DescriptionDraft.Substring(0, 50));
        Assert.NotEqual(mug.DescriptionDraft.Substring(0, 50), wallet.DescriptionDraft.Substring(0, 50));
    }

    // =========================================================================
    // 4. TEST: Gemini Prompt & JSON Yanıt Ayrıştırıcı Güvenliği
    // =========================================================================
    [Fact]
    public void CategoryResponseParser_HandlesGeminiMarkdownOutputWithAlternatives()
    {
        var geminiResponse = """
            ```json
            {
              "taxonomy_id": 1042,
              "category_path": "Home & Living > Lighting > Night Lights",
              "confidence_score": 96,
              "reasoning": "Ürün astronot figürlü çocuk odası LED gece lambasıdır.",
              "alternatives": [
                {
                  "taxonomy_id": 1041,
                  "category_path": "Home & Living > Lighting > Lamps",
                  "confidence_score": 88
                }
              ]
            }
            ```
            """;

        var parsed = CategoryResponseParser.Parse(geminiResponse, "Gemini");

        Assert.NotNull(parsed);
        Assert.Equal(1042, parsed.TaxonomyId);
        Assert.Equal("Home & Living > Lighting > Night Lights", parsed.CategoryPath);
        Assert.Equal(96, parsed.ConfidenceScore);
        Assert.Equal("Gemini", parsed.ProviderUsed);
        Assert.Single(parsed.Alternatives);
        Assert.Equal(1041, parsed.Alternatives[0].TaxonomyId);
    }

    [Fact]
    public void GenerateComparativeAuditReport_ForInspection()
    {
        var optimizer = new ListingOptimizationService();

        var products = new (string Title, string Desc, string[] Tags, string Target, long ExpTax)[]
        {
            (
                "Astronaut LED Night Light - Bedside Table Lamp for Kids Nursery",
                "Astronaut figurine night light with multi-color LED glow, USB powered, 15cm height. Cosmic bedroom ambiance.",
                new[] { "astronaut", "lamp", "space", "gift" },
                "astronaut night light",
                1042
            ),
            (
                "Handmade Ceramic Mountain Coffee Mug - 12oz Pottery Tea Cup",
                "Speckled clay mug with reactive mountain glaze, 12oz capacity, dishwasher safe. Cozy morning brew.",
                new[] { "mug", "coffee", "cup", "pottery" },
                "ceramic coffee mug",
                943
            ),
            (
                "Full Grain Leather Bifold Wallet - Slim Card Holder for Men",
                "Minimalist leather bifold wallet with 6 card slots and cash pocket. Full grain genuine leather, slim pocket carry.",
                new[] { "wallet", "leather", "cardholder" },
                "leather bifold wallet",
                142
            ),
            (
                "3D Printed Dragon Headphone Stand - Gaming Desk Audio Holder",
                "Fantasy dragon headset holder for gaming battlestation desk organization. Fits all over-ear gaming headphones.",
                new[] { "headphone stand", "gaming", "desk decor" },
                "dragon headphone stand",
                2079
            )
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== MAĞAZA AI ANALİZİ VE HIZLI ÜRÜN EKLE TEST RAPORU ===");

        foreach (var p in products)
        {
            var cat = LocalCategoryHeuristics.SuggestFromText(p.Title, p.Desc, string.Join(' ', p.Tags));
            var opt = optimizer.Optimize(new ListingOptimizationInput(p.Title, p.Desc, p.Tags, p.Target));

            sb.AppendLine($"\n--- ÜRÜN: {p.Title} ---");
            sb.AppendLine($"KATEGORİ (Taxonomy ID): {cat.TaxonomyId} - {cat.CategoryPath} (Güven: %{cat.ConfidenceScore})");
            sb.AppendLine($"SEO BAŞLIK 1: {opt.TitleSuggestions.FirstOrDefault()} ({opt.TitleSuggestions.FirstOrDefault()?.Length} Karakter)");
            sb.AppendLine($"13 ADET TAG: {string.Join(", ", opt.TagSuggestions)}");
            sb.AppendLine($"GİRİŞ AÇIKLAMASI (İlk 150 Karakter): {opt.DescriptionDraft.Substring(0, Math.Min(150, opt.DescriptionDraft.Length))}...");
        }

        System.IO.File.WriteAllText("ai_audit_test_report.txt", sb.ToString());
        Assert.True(System.IO.File.Exists("ai_audit_test_report.txt"));
    }
}
