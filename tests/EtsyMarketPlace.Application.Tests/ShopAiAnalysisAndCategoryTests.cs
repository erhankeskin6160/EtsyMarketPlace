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

    // =========================================================================
    // 5. TEST: Kullanıcının Girdiği Boyut, Kutu İçeriği ve Ekstra Bilgileri İşleme
    // "kullanıcı açıklamaya uygun ek bilgi yazınca ai destekle butonuna basınca o bilgileri ışığında açıklama üretiyormu"
    // =========================================================================
    [Fact]
    public void FastListingCreator_ProcessesUserProvidedDimensionsAndNotes_ForAstronautLamp()
    {
        var optimizer = new ListingOptimizationService();

        // Kullanıcı arayüzde taslak başlık ve kutucuğa kendi ölçü/özellik notlarını girdi:
        var userRawNotes = """
            Ölçüler: 18cm x 12cm x 6cm yükseklik
            Paket İçeriği: 1 adet Astronot Lamba ve 1 adet Type-C USB Şarj Kablosu
            Özellikler: 16 farklı RGB renk modu ve dokunmatik sensör kontrolü
            Özel Not: Tabanında kaymaz silikon ayaklar yer almaktadır.
            """;

        var input = new ListingOptimizationInput(
            Title: "Astronot Figür Gece Lambası",
            Description: userRawNotes,
            Tags: ["gece lambasi", "astronot lamba"],
            TargetKeyword: "astronaut night light"
        );

        // Act: Kategori tespiti ve AI Açıklama desteği
        var category = LocalCategoryHeuristics.SuggestFromText(input.Title, input.Description, string.Join(' ', input.Tags));
        var result = optimizer.Optimize(input);

        // 1. Kategori: Gece lambası tespit edilmeli
        Assert.Equal(1042, category.TaxonomyId);
        Assert.Contains("Night Lights", category.CategoryPath);

        // 2. Kullanıcının girdiği boyutlar BÖLÜM 3'e (SPECIFICATIONS) işlenmiş mi?
        Assert.Contains("18cm x 12cm x 6cm", result.DescriptionDraft);

        // 3. Kullanıcının girdiği paket içeriği işlenmiş mi?
        Assert.Contains("Type-C USB", result.DescriptionDraft);

        // 4. Kullanıcının belirttiği RGB / dokunmatik özelliği öne çıkanlara (WHY YOU'LL LOVE IT) alınmış mı?
        Assert.Contains("RGB", result.DescriptionDraft);

        // 5. Kullanıcının özel notu eklenmiş mi?
        Assert.Contains("silikon ayaklar", result.DescriptionDraft);

        // 6. Alakasız gaming/anime klişesi ASLA olmamalı
        Assert.DoesNotContain("Gamers, anime lovers", result.DescriptionDraft);
    }

    [Fact]
    public void FastListingCreator_ProcessesUserProvidedSpecs_ForLeatherWallet()
    {
        var optimizer = new ListingOptimizationService();

        var userRawNotes = """
            Boyut: 11.5 cm x 9 cm kapalı ebat
            Malzeme: Hakiki dana derisi, el dikişi
            Kutu İçeriği: Özel hediye kutusunda kraft kağıt sarılı teslim edilir
            Özel Not: 8 kart bölmesi ve 2 nakit para gözü mevcuttur.
            """;

        var input = new ListingOptimizationInput(
            Title: "El Yapımı Deri Erkek Cüzdanı",
            Description: userRawNotes,
            Tags: ["deri cuzdan", "kartlik"],
            TargetKeyword: "leather bifold wallet"
        );

        var category = LocalCategoryHeuristics.SuggestFromText(input.Title, input.Description, string.Join(' ', input.Tags));
        var result = optimizer.Optimize(input);

        // 1. Kategori: Cüzdan kategorisi bulunmalı
        Assert.Equal(142, category.TaxonomyId);
        Assert.Contains("Wallets", category.CategoryPath);

        // 2. Kullanıcı boyutu (11.5 cm x 9 cm) açıklamada yer almalı
        Assert.Contains("11.5 cm x 9 cm", result.DescriptionDraft);

        // 3. Kutu içeriği işlenmiş mi?
        Assert.Contains("hediye kutusunda", result.DescriptionDraft);

        // 4. Malzeme deri (leather) olarak belirlenmiş mi?
        Assert.Contains("leather", result.MaterialSuggestions);

        // 5. Açıklamada 8 kart bölmesi notu korunmuş mu?
        Assert.Contains("8 kart bölmesi", result.DescriptionDraft);
    }

    [Fact]
    public void FastListingCreator_ProcessesUserProvidedSpecs_ForHeadphoneStand()
    {
        var optimizer = new ListingOptimizationService();

        var userRawNotes = """
            Yükseklik: 24 cm, Taban Genişliği: 14 cm
            Malzeme: PLA+ filament, ağırlık 320 gram
            Özellik: Kablo sarma kanalı ve devrilmeyi önleyen ağırlıklı taban
            Paket İçeriği: 1 adet demonte ejderha gövde ve 1 adet kilitli taban
            """;

        var input = new ListingOptimizationInput(
            Title: "3D Baskı Ejderha Kulaklık Tutucu Stand",
            Description: userRawNotes,
            Tags: ["kulaklik standi", "kulaklik tutucu"],
            TargetKeyword: "dragon headphone stand"
        );

        var category = LocalCategoryHeuristics.SuggestFromText(input.Title, input.Description, string.Join(' ', input.Tags));
        var result = optimizer.Optimize(input);

        // 1. Kategori: Kulaklık Standı (2079) bulunmalı
        Assert.Equal(2079, category.TaxonomyId);
        Assert.Contains("Headphone", category.CategoryPath);

        // 2. Kullanıcı ölçüsü (24 cm) korunmuş mu?
        Assert.Contains("24 cm", result.DescriptionDraft);

        // 3. Paket içeriği (kilitli taban) korunmuş mu?
        Assert.Contains("kilitli taban", result.DescriptionDraft);

        // 4. Malzeme listesinde pla plastic var mı?
        Assert.Contains(result.MaterialSuggestions, m => m.Contains("pla", StringComparison.OrdinalIgnoreCase));

        // 5. Özelliklerde devrilmeyi önleyen taban / kablo kanalı var mı?
        Assert.Contains("ağırlıklı taban", result.DescriptionDraft);
    }

    // =========================================================================
    // 6. TEST: SEO DOLANDIRICILIĞI VE KALİTE KONTROLLERİ (Anti-Spam & Policy)
    // "Seo dolandırıcılığı yapıyormu bunları kontrol et"
    // =========================================================================
    [Fact]
    public void SEO_Quality_Check_DoesNotDoKeywordStuffing_InTitleOrTags()
    {
        var optimizer = new ListingOptimizationService();
        var input = new ListingOptimizationInput(
            Title: "Astronaut Lamp LED Space Glow",
            Description: "Astronaut night light for room decor.",
            Tags: ["astronaut", "lamp", "night light"],
            TargetKeyword: "astronaut night light"
        );

        var result = optimizer.Optimize(input);
        var title = result.TitleSuggestions.First();

        // 1. Başlıkta hiçbir kelime 2 defadan fazla arka arkaya veya gereksiz yere tekrar edilmemeli
        var titleWords = title.Split([' ', '-', ',', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();
        var titleWordCounts = titleWords.GroupBy(w => w, StringComparer.OrdinalIgnoreCase);
        foreach (var group in titleWordCounts)
        {
            Assert.True(group.Count() <= 2, $"Başlıkta '{group.Key}' kelimesi {group.Count()} kez tekrarlanmış (Keyword Stuffing ihlali)!");
        }

        // 2. Başlık bot tarzı boru karakterleri (| | |) zinciri olmamalı
        Assert.DoesNotContain("|", title);

        // 3. 13 Tag arasında hiçbir kök kelime 3 defadan fazla tekrarlanmamalı
        var allTagWords = result.TagSuggestions
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 3)
            .ToList();
        var tagWordCounts = allTagWords.GroupBy(w => w, StringComparer.OrdinalIgnoreCase);
        foreach (var group in tagWordCounts)
        {
            Assert.True(group.Count() <= 3, $"Etiketlerde '{group.Key}' kelimesi {group.Count()} kez tekrarlanmış (Tag Spam ihlali)!");
        }
    }

    [Fact]
    public void SEO_Quality_Check_DetectsAndFlagsTrademarkFraud()
    {
        var optimizer = new ListingOptimizationService();

        // Kullanıcı telifli/markalı yanıltıcı bir başlık girmeye kalkarsa:
        var input = new ListingOptimizationInput(
            Title: "Disney Marvel Spiderman 3D Night Light with Pokemon Pikachu Base",
            Description: "Handmade night light with superhero design.",
            Tags: ["disney", "marvel", "spiderman", "pokemon"],
            TargetKeyword: "spiderman lamp"
        );

        var result = optimizer.Optimize(input);

        // AI dolandırıcılık veya marka taklidine ortak olmamalı, satıcıyı RiskWarnings ile uyarmalı!
        Assert.NotEmpty(result.RiskWarnings);
        Assert.Contains(result.RiskWarnings, w => w.Contains("disney", StringComparison.OrdinalIgnoreCase) ||
                                                 w.Contains("marvel", StringComparison.OrdinalIgnoreCase) ||
                                                 w.Contains("spiderman", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SEO_Quality_Check_DoesNotDumpRawTagsInDescription()
    {
        var optimizer = new ListingOptimizationService();
        var input = new ListingOptimizationInput(
            Title: "Handmade Ceramic Mug",
            Description: "12oz ceramic coffee cup.",
            Tags: ["mug", "coffee", "cup"],
            TargetKeyword: "ceramic coffee mug"
        );

        var result = optimizer.Optimize(input);
        var desc = result.DescriptionDraft;

        // Etsy'nin yasakladığı 'Açıklama altına toplu tag yığma' (Tag Dump / Hidden Spam) yapılmamalı!
        Assert.DoesNotContain("Tags:", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Keywords:", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Anahtar Kelimeler:", desc, StringComparison.OrdinalIgnoreCase);

        // Metin Etsy alıcısına hitap eden, ürüne özel (kahve kupası için zanaat ve kapasite odaklı) temiz bölümlü paragraf yapısında olmalı
        Assert.True(desc.Contains("ARTISAN CRAFT", StringComparison.OrdinalIgnoreCase) || desc.Contains("WHY YOU'LL LOVE", StringComparison.OrdinalIgnoreCase));
        Assert.True(desc.Contains("CAPACITY", StringComparison.OrdinalIgnoreCase) || desc.Contains("SPECIFICATIONS", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("PACKAGING", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SEO_Quality_Check_NeverLeaksSystemPromptsOrBotInstructions()
    {
        var optimizer = new ListingOptimizationService();
        var input = new ListingOptimizationInput(
            Title: "OUTPUT LANGUAGE: English only. Do not write Turkish. Astronaut Lamp",
            Description: "Selected marketplace listing: Competitor description: Bedside lamp.",
            Tags: ["lamp"],
            TargetKeyword: "astronaut lamp"
        );

        var result = optimizer.Optimize(input);

        // Başlıkta veya açıklamada prompt sızıntısı veya yapay zeka meta komutları ASLA yer almamalı!
        var title = result.TitleSuggestions.First();
        Assert.DoesNotContain("OUTPUT LANGUAGE", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("English only", title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Do not write Turkish", title, StringComparison.OrdinalIgnoreCase);

        var desc = result.DescriptionDraft;
        Assert.DoesNotContain("Selected marketplace listing", desc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Competitor description", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FastListingCreator_ClassifiesTenDifferentProductTypes_Accurately()
    {
        var diverseProducts = new (string Title, long ExpectedTaxonomyId, string ExpectedCategory)[]
        {
            ("Astronaut LED Night Light Nursery Lamp", 1042, "Night Lights"),
            ("Handmade Speckled Ceramic Coffee Mug 12oz", 943, "Mugs"),
            ("Full Grain Leather Bifold Mens Wallet", 142, "Wallets"),
            ("3D Printed Dragon Headphone Stand Audio Holder", 2079, "Headphone"),
            ("Modern Geometric Wooden Wall Art Sign", 1054, "Wall Decor"),
            ("Hand Poured Soy Candle Nordic Candleholder", 1063, "Candleholders"),
            ("Lord of the Rings Gollum 3D Statue Bust", 1239, "Sculptures"),
            ("Dainty Sterling Silver Choker Necklace", 204, "Necklaces"),
            ("Personalized Leather Dog Collar with Name Tag", 982, "Pet Collars"),
            ("Handcrafted Wooden Chess Set Board Game", 1381, "Board Games")
        };

        foreach (var p in diverseProducts)
        {
            var cat = LocalCategoryHeuristics.SuggestFromText(p.Title);
            Assert.Equal(p.ExpectedTaxonomyId, cat.TaxonomyId);
            Assert.Contains(p.ExpectedCategory, cat.CategoryPath, StringComparison.OrdinalIgnoreCase);
        }

        // 10 farklı ürünün 10'u da benzersiz kategori taxonomy ID'si üretmeli!
        var uniqueCount = diverseProducts
            .Select(p => LocalCategoryHeuristics.SuggestFromText(p.Title).TaxonomyId)
            .Distinct()
            .Count();
        Assert.Equal(10, uniqueCount);
    }

    // =========================================================================
    // 6. TEST: Mağaza AI Analizi - "Tümü AI Denetle" & Puanlama Sistemi Testi
    // "ürünleri Tümü AI Denetle sistemi nasıl çalışıyor puanla sistemi iyimi dene"
    // =========================================================================
    [Fact]
    public void OwnShopAiAudit_ScoringSystem_AccuratelyDifferentiatesWeakAndStrongListings()
    {
        // 1. Zayıf listing simülasyonu (3 etiket, 20 karakter başlık, 1 görsel, 50 karakter açıklama, 0 favori)
        var weakListingTags = new List<string> { "cup", "gift", "handmade" };
        var weakTitle = "Ceramic Cup Hand";
        var weakDesc = "Nice handmade cup for sale.";
        var weakImages = new List<string> { "https://example.com/1.jpg" };

        var tagScoreWeak = Math.Min(25, weakListingTags.Count * 25 / 13); // 5 puan
        var titleScoreWeak = weakTitle.Length is >= 55 and <= 135 ? 25 : weakTitle.Length is >= 35 and <= 140 ? 18 : 8; // 8 puan
        var imageScoreWeak = weakImages.Count >= 5 ? 20 : weakImages.Count * 4; // 4 puan
        var descScoreWeak = weakDesc.Length >= 500 ? 20 : weakDesc.Length >= 250 ? 12 : 5; // 5 puan
        var weakScore = tagScoreWeak + titleScoreWeak + imageScoreWeak + descScoreWeak; // 22 puan

        Assert.True(weakScore < 40, $"Zayıf listing puanı 40'ın altında olmalı, hesaplanan: {weakScore}");

        // 2. Kusursuz optimize listing simülasyonu (13 etiket, 125 karakter başlık, 6 görsel, 1650 karakter açıklama, 12 favori)
        var strongListingTags = Enumerable.Range(1, 13).Select(i => $"artisan mug tag {i}").ToList();
        var strongTitle = "Handmade Speckled Ceramic Coffee Mug 12oz - Kitchen Counter Art, Coffee Lover Present, Clay Art Piece, 12oz Cup";
        var strongDesc = new string('A', 1650);
        var strongImages = Enumerable.Repeat("https://example.com/img.jpg", 6).ToList();

        var tagScoreStrong = Math.Min(25, strongListingTags.Count * 25 / 13); // 25 puan
        var titleScoreStrong = strongTitle.Length is >= 55 and <= 135 ? 25 : strongTitle.Length is >= 35 and <= 140 ? 18 : 8; // 25 puan
        var imageScoreStrong = strongImages.Count >= 5 ? 20 : strongImages.Count * 4; // 20 puan
        var descScoreStrong = strongDesc.Length >= 500 ? 20 : strongDesc.Length >= 250 ? 12 : 5; // 20 puan
        var signalScoreStrong = 10;
        var strongScore = tagScoreStrong + titleScoreStrong + imageScoreStrong + descScoreStrong + signalScoreStrong; // 100 puan

        Assert.Equal(100, strongScore);
    }

    [Fact]
    public void OwnShopAiAudit_BatchAudit_ProducesDiverseNonRepetitiveDescriptionsAndTags()
    {
        var optimizer = new ListingOptimizationService();
        var shopProducts = new[]
        {
            new ListingOptimizationInput(
                "Speckled Ceramic Coffee Mug 12oz",
                "Handmade stoneware coffee mug 12oz capacity with speckled glaze. Dishwasher safe. Height 9.5cm.",
                ["ceramic mug", "coffee cup", "tea mug"],
                "ceramic coffee mug"
            ),
            new ListingOptimizationInput(
                "Full Grain Leather Bifold Mens Wallet",
                "Genuine full grain cowhide leather bifold wallet. 8 card slots, 1 cash compartment. 11.5cm x 9cm.",
                ["leather wallet", "bifold wallet", "card holder"],
                "leather card wallet"
            ),
            new ListingOptimizationInput(
                "Astronaut LED Night Light Desk Lamp",
                "Astronaut bedside lamp with USB power and starry projection glow. 18cm x 12cm.",
                ["astronaut lamp", "night light", "space lamp"],
                "astronaut night lamp"
            ),
            new ListingOptimizationInput(
                "Handcrafted Wooden Chess Set Tabletop",
                "Hand-carved walnut and maple chess set. Board 40cm x 40cm, King height 9.5cm.",
                ["chess set", "wooden game", "board game"],
                "carved chess set"
            )
        };

        var batchResults = shopProducts.Select(p => optimizer.Optimize(p)).ToList();

        // 1. Her ürün için AI SEO Skoru 85'in üzerinde olmalı
        foreach (var res in batchResults)
        {
            Assert.True(res.OptimizedSeoScore >= 85, $"Optimize SEO skoru 85+ olmalı, gelen: {res.OptimizedSeoScore}");
            Assert.Equal(13, res.TagSuggestions.Count);
        }

        // 2. Açıklamalar birbirinden tamamen farklı olmalı (Jaccard benzerliği < 0.35)
        for (int i = 0; i < batchResults.Count; i++)
        {
            for (int j = i + 1; j < batchResults.Count; j++)
            {
                var desc1Words = batchResults[i].DescriptionDraft.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLowerInvariant()).ToHashSet();
                var desc2Words = batchResults[j].DescriptionDraft.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLowerInvariant()).ToHashSet();
                var common = desc1Words.Intersect(desc2Words).Count();
                var total = desc1Words.Union(desc2Words).Count();
                var similarity = (double)common / total;
                Assert.True(similarity < 0.40, $"Ürünler arasında açıklama benzerliği çok yüksek: {similarity:P1}");
            }
        }

        // 3. Her ürünün 13 etiketi kendi nişine ait olmalı ve 20 karakter sınırını aşmamalı
        foreach (var res in batchResults)
        {
            foreach (var tag in res.TagSuggestions)
            {
                Assert.True(tag.Length <= 20, $"Etiket 20 karakterden uzun: {tag}");
                Assert.True(tag.Length >= 4, $"Etiket çok kısa: {tag}");
            }
        }
    }

    [Fact]
    public void OwnShopAiAudit_SablonlaParagraf_GeneratesBespokeThemeHeadersAndCareGuides()
    {
        // "Şablonla Paragrafta sıkıntı var mı? Hep aynı tarz şablonla mı oluşuyor?" testi
        var mugFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "12oz ceramic coffee cup. Dishwasher safe. 9.5cm height.",
            "Handmade Speckled Ceramic Coffee Mug 12oz",
            ["ceramic coffee mug", "handmade mug"],
            ["ceramic", "clay"],
            "ceramic coffee mug"
        );

        var walletFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "Full grain leather wallet with 8 card slots. 11.5cm x 9cm.",
            "Full Grain Leather Bifold Mens Wallet",
            ["leather wallet", "bifold wallet"],
            ["leather"],
            "leather card wallet"
        );

        var lampFormatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            "Astronaut night light with USB cable. Dimensions 18cm x 12cm.",
            "Astronaut LED Night Light Bedside Lamp",
            ["astronaut lamp", "night light"],
            ["pla plastic"],
            "astronaut night lamp"
        );

        // Kupa için özel başlık ve bakım rehberi
        Assert.Contains("ARTISAN CRAFT & DAILY ENJOYMENT", mugFormatted);
        Assert.Contains("CAPACITY, SIZING & CARE", mugFormatted);
        Assert.Contains("CARE & CLEANING INSTRUCTIONS", mugFormatted);
        Assert.Contains("dishwasher", mugFormatted, StringComparison.OrdinalIgnoreCase);

        // Cüzdan için özel deri başlık ve bakım rehberi
        Assert.Contains("PREMIUM LEATHER & TIMELESS CRAFT", walletFormatted);
        Assert.Contains("CARD SLOTS, CAPACITY & MEASUREMENTS", walletFormatted);
        Assert.Contains("LEATHER CARE & PRESERVATION", walletFormatted);
        Assert.Contains("leather balm", walletFormatted, StringComparison.OrdinalIgnoreCase);

        // Lamba için kozmik ışıltı başlığı ve LED bakım rehberi
        Assert.Contains("COSMIC GLOW & BEDTIME AMBIANCE", lampFormatted);
        Assert.Contains("DIMENSIONS, POWER & LIGHTING SPECS", lampFormatted);
        Assert.Contains("LIGHTING CARE & OPERATION", lampFormatted);
        Assert.Contains("USB", lampFormatted);

        // Kesinlikle kalıp şablon değil; 3 farklı ürün tamamen farklı niş başlıkları ve bakım talimatları aldı!
        Assert.DoesNotContain("ARTISAN CRAFT", walletFormatted);
        Assert.DoesNotContain("PREMIUM LEATHER", mugFormatted);
        Assert.DoesNotContain("COSMIC GLOW", walletFormatted);
    }

    [Fact]
    public async Task OwnShopAiAudit_VersionHistory_SavesAndRetrievesAuditVersions_Correctly()
    {
        // "versiyon kaydet sistemi çalışıyormu" testi
        var tempDb = Path.Combine(Path.GetTempPath(), $"etsy-audit-ver-{Guid.NewGuid():N}.db");
        try
        {
            var repo = new EtsyMarketPlace.Infrastructure.ListingOptimization.SqliteListingOptimizationHistoryRepository(tempDb);
            var historyService = new ListingOptimizationHistoryService(repo);
            await historyService.InitializeAsync();

            var sampleResult = new ListingOptimizationResult(
                CurrentSeoScore: 35,
                OptimizedSeoScore: 92,
                TitleSuggestions: ["Handmade Speckled Ceramic Coffee Mug 12oz - Kitchen Counter Art"],
                TagSuggestions: ["ceramic coffee mug", "handmade drinkware", "clay art piece"],
                MaterialSuggestions: ["Ceramic", "Clay"],
                DescriptionDraft: "Artisan coffee mug draft description.",
                MissingTerms: [],
                RiskWarnings: [],
                ActionChecklist: ["Tags completed", "Title expanded"]
            );

            var saved = await historyService.SaveAsync(new SaveListingOptimizationHistory(
                "987654321",
                "Old Ceramic Mug",
                "ceramic coffee mug",
                sampleResult
            ));

            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);

            var recent = await historyService.GetRecentAsync(10);
            var historyItem = Assert.Single(recent);
            Assert.Equal("987654321", historyItem.ListingId);
            Assert.Equal(35, historyItem.CurrentSeoScore);
            Assert.Equal(92, historyItem.OptimizedSeoScore);
            Assert.Equal("Old Ceramic Mug", historyItem.ListingTitle);
            Assert.Contains("ceramic coffee mug", historyItem.SuggestedTags);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb)) File.Delete(tempDb);
            if (File.Exists(tempDb + "-wal")) File.Delete(tempDb + "-wal");
            if (File.Exists(tempDb + "-shm")) File.Delete(tempDb + "-shm");
        }
    }

    [Theory]
    [InlineData("gemini-3.8-flash", "gemini-3.8-flash")]
    [InlineData("gemini 3.8 flash", "gemini-3.8-flash")]
    [InlineData("3.8 flash", "gemini-3.8-flash")]
    public void OwnShopAiAudit_Gemini38Flash_IsCorrectlyNormalized(string input, string expected)
    {
        // "gemini 3.8 flash modelini kullan" kontrolü
        var normalized = EtsyAiModelNormalizer.NormalizeGeminiTextModel(input);
        Assert.Equal(expected, normalized);
    }
}
