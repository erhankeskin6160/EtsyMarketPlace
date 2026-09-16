namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// "Ürün Bul - Taslak Hazırla" (Product Discovery Listing Creator / Full Tık AI)
/// modülünün ürün çeşitliliği, ürün özelliklerine sadakat, tag uzunlukları (max 20 char)
/// ve SEO anti-fraud güvenlik mekanizmalarını doğrulayan izole test paketi.
/// </summary>
public sealed class ProductDiscoveryDraftAiTests
{
    private readonly ListingOptimizationService _optimizer = new();
    private readonly ITestOutputHelper? _output;

    public ProductDiscoveryDraftAiTests(ITestOutputHelper? output = null)
    {
        _output = output;
    }

    private sealed record PulledListingMock(
        string Title,
        string Description,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> Materials,
        string TargetKeyword,
        string TaxonomyCategory
    );

    private static readonly PulledListingMock[] DiverseSampleListings =
    [
        new(
            Title: "Astronaut LED Night Light Nursery Bedside Lamp",
            Description: "A soothing celestial bedtime companion.\nDimensions: 18cm x 12cm x 6cm\nMaterials: PLA, ABS plastic\nPackage: Lamp, USB Type-C Cable, user guide.\nNote: Safe for nurseries with cool-touch LED.",
            Tags: ["night light", "bedside lamp", "kids lamp"],
            Materials: ["PLA", "Plastic"],
            TargetKeyword: "astronaut night lamp",
            TaxonomyCategory: "Home & Living > Lighting > Night Lights"
        ),
        new(
            Title: "Handmade Speckled Ceramic Coffee Mug 12oz",
            Description: "Artisan stoneware mug crafted for everyday enjoyment.\nCapacity: 12 oz (350 ml)\nDimensions: Height 9.5 cm, diameter 8.5 cm\nMaterials: Stoneware clay, ceramic food-safe glaze\nDishwasher and microwave safe.",
            Tags: ["coffee mug", "ceramic mug", "tea cup"],
            Materials: ["Ceramic", "Clay"],
            TargetKeyword: "ceramic coffee mug",
            TaxonomyCategory: "Home & Living > Kitchen & Dining > Drinkware > Mugs"
        ),
        new(
            Title: "Full Grain Leather Bifold Mens Wallet",
            Description: "Classic handcrafted pocket wallet.\nDimensions: 11.5 cm x 9 cm\nCapacity: 8 card slots, 1 full cash compartment\nMaterials: 100% Genuine full grain cowhide leather\nIncludes gift box.",
            Tags: ["leather wallet", "mens wallet", "card holder"],
            Materials: ["Leather"],
            TargetKeyword: "leather card wallet",
            TaxonomyCategory: "Bags & Purses > Wallets & Money Clips"
        ),
        new(
            Title: "3D Printed Dragon Headphone Stand Audio Gear Desk Rest",
            Description: "Sculpted headphone holder for audiophiles and streamers.\nDimensions: Height 24 cm, base width 13 cm\nMaterials: High density resin\nFeatures: Anti-slip silicone feet on bottom.",
            Tags: ["headphone stand", "headset stand", "desk holder"],
            Materials: ["Resin"],
            TargetKeyword: "headphone desk stand",
            TaxonomyCategory: "Electronics & Accessories > Audio > Headphone & Headset Stands"
        ),
        new(
            Title: "Modern Geometric Wooden Wall Art Sign",
            Description: "Laser cut geometric wood wall hanging decor.\nDimensions: 45 cm x 25 cm x 0.8 cm\nMaterials: Natural walnut wood, eco friendly varnish\nIncludes pre-installed metal hanging hardware.",
            Tags: ["wall decor", "wood wall art", "wall sign"],
            Materials: ["Wood"],
            TargetKeyword: "wooden wall sign",
            TaxonomyCategory: "Home & Living > Home Decor > Wall Decor"
        ),
        new(
            Title: "Handcrafted Wooden Chess Set Board Game Tabletop",
            Description: "Carved wooden chess set for strategy lovers and decor.\nDimensions: Board 30 cm x 30 cm, King height 6.5 cm\nMaterials: Solid beech wood, velvet base on pieces\nIncludes wooden storage board case.",
            Tags: ["chess set", "board game", "chess board"],
            Materials: ["Wood"],
            TargetKeyword: "wooden chess set",
            TaxonomyCategory: "Toys & Games > Games & Puzzles > Board Games"
        ),
        new(
            Title: "Personalized Leather Dog Collar with Nameplate",
            Description: "Custom heavy duty pet collar for dogs.\nDimensions: Neck girth 35 cm - 45 cm, width 2.5 cm\nMaterials: Genuine leather, solid brass buckle\nNote: Laser engraved brass nameplate included.",
            Tags: ["dog collar", "pet collar", "leather collar"],
            Materials: ["Leather", "Brass"],
            TargetKeyword: "custom dog collar",
            TaxonomyCategory: "Pet Supplies > Pet Collars & Leashes"
        )
    ];

    [Fact]
    public void FullTikAi_DifferentProducts_ProduceCompletelyDifferentDescriptionsAndStyles()
    {
        // "Bütün ürünlere hep aynı açıklamayı mı yazıyor, hep aynı tarz mı yazıyor?" kontrolü
        var results = new List<(PulledListingMock Mock, ListingOptimizationResult Result)>();

        foreach (var mock in DiverseSampleListings)
        {
            var input = new ListingOptimizationInput(
                mock.Title,
                mock.Description,
                mock.Tags,
                mock.TargetKeyword
            );
            var result = _optimizer.Optimize(input);
            results.Add((mock, result));
        }

        // 1. Hiçbir 2 ürünün açıklaması birbirinin kopyası olmamalı
        for (int i = 0; i < results.Count; i++)
        {
            for (int j = i + 1; j < results.Count; j++)
            {
                var desc1 = results[i].Result.DescriptionDraft;
                var desc2 = results[j].Result.DescriptionDraft;
                Assert.NotEqual(desc1, desc2);

                // Ortak yapısal bölüm başlıklarını ve kargo metinlerini çıkarıp asıl ürün içeriği benzerliğine odaklanalım
                var boilerplate = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "why", "you'll", "love", "it", "specifications", "details", "perfect", "for",
                    "packaging", "shipping", "custom", "requests", "questions", "materials", "dimensions",
                    "finish", "package", "includes", "note", "carefully", "wrapped", "protective",
                    "safe", "worldwide", "arrival", "tracked", "dispatch", "sent", "directly", "email",
                    "shipment", "shoppers", "searching", "reach", "anytime", "happy", "help", "order",
                    "looking", "color", "size", "feel", "free", "with", "this", "from", "your"
                };

                var words1 = desc1.Split([' ', '\r', '\n', '•', ',', '.', ':', ';', '!', '?', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant()).ToHashSet();
                var words2 = desc2.Split([' ', '\r', '\n', '•', ',', '.', ':', ';', '!', '?', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant()).ToHashSet();

                var productWords1 = words1.Where(w => !boilerplate.Contains(w) && w.Length > 2).ToHashSet();
                var productWords2 = words2.Where(w => !boilerplate.Contains(w) && w.Length > 2).ToHashSet();

                var intersection = productWords1.Intersect(productWords2).Count();
                var union = productWords1.Union(productWords2).Count();
                double jaccardSimilarity = union > 0 ? (double)intersection / union : 0;

                Assert.True(jaccardSimilarity < 0.35,
                    $"Ürün {results[i].Mock.Title} ile {results[j].Mock.Title} arasında aşırı içerik benzerliği tespit edildi: {jaccardSimilarity:P1}");
            }
        }

        // 2. Her ürünün tarzı ve kancası kendi kategorisine özgü olmalı
        var astronautDesc = results.First(r => r.Mock.TargetKeyword.Contains("astronaut")).Result.DescriptionDraft;
        Assert.Contains("cosmic", astronautDesc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("space", astronautDesc, StringComparison.OrdinalIgnoreCase);

        var walletDesc = results.First(r => r.Mock.TargetKeyword.Contains("wallet")).Result.DescriptionDraft;
        Assert.Contains("everyday essentials", walletDesc, StringComparison.OrdinalIgnoreCase);

        var chessDesc = results.First(r => r.Mock.TargetKeyword.Contains("chess")).Result.DescriptionDraft;
        Assert.Contains("game night", chessDesc, StringComparison.OrdinalIgnoreCase);

        var petDesc = results.First(r => r.Mock.TargetKeyword.Contains("dog")).Result.DescriptionDraft;
        Assert.Contains("companion", petDesc, StringComparison.OrdinalIgnoreCase);

        // 3. Bölüm başlıkları (Headings) her ürüne özel olmalı, tek bir kalıp robotik olarak tekrarlanmamalı
        var mugDesc = results.First(r => r.Mock.TargetKeyword.Contains("ceramic")).Result.DescriptionDraft;
        Assert.Contains("ARTISAN CRAFT & DAILY ENJOYMENT", mugDesc);
        Assert.Contains("CAPACITY, SIZING & CARE", mugDesc);

        Assert.Contains("PREMIUM LEATHER & TIMELESS CRAFT", walletDesc);
        Assert.Contains("CARD SLOTS, CAPACITY & MEASUREMENTS", walletDesc);

        Assert.Contains("COSMIC GLOW & BEDTIME AMBIANCE", astronautDesc);
        Assert.Contains("DIMENSIONS, POWER & LIGHTING SPECS", astronautDesc);

        var standDesc = results.First(r => r.Mock.TargetKeyword.Contains("headphone")).Result.DescriptionDraft;
        Assert.Contains("BATTLESTATION STYLING & GEAR REST", standDesc);
        Assert.Contains("MEASUREMENTS & STABILITY DETAILS", standDesc);

        Assert.Contains("HAND-CARVED WOODWORK & STRATEGY", chessDesc);
        Assert.Contains("BOARD & PIECE MEASUREMENTS", chessDesc);

        Assert.Contains("PET COMFORT & DURABLE HARDWARE", petDesc);
        Assert.Contains("SIZING & COLLAR ADJUSTMENT", petDesc);
    }

    [Fact]
    public void FullTikAi_FaithfulToPulledProductSpecifications_DimensionsMaterialsAndNotes()
    {
        // "Çekilen ürün bilgilerine sadık kalıyor mu? Boyut, malzeme, kutu içeriği korunuyor mu?" kontrolü
        foreach (var mock in DiverseSampleListings)
        {
            var input = new ListingOptimizationInput(
                mock.Title,
                mock.Description,
                mock.Tags,
                mock.TargetKeyword
            );
            var result = _optimizer.Optimize(input);
            var desc = result.DescriptionDraft;

            // Mock açıklamalarındaki teknik detayların (cm, oz, ml, malzeme) yapay zeka tarafından korunduğu teyit edilir:
            if (mock.Description.Contains("18cm x 12cm x 6cm"))
            {
                Assert.Contains("18cm x 12cm x 6cm", desc);
                Assert.Contains("USB Type-C Cable", desc);
            }
            if (mock.Description.Contains("11.5 cm x 9 cm"))
            {
                Assert.Contains("11.5 cm x 9 cm", desc);
                Assert.Contains("8 card slots", desc);
            }
            if (mock.Description.Contains("24 cm"))
            {
                Assert.Contains("24 cm", desc);
                Assert.Contains("silicone feet", desc);
            }
            if (mock.Description.Contains("45 cm x 25 cm"))
            {
                Assert.Contains("45 cm x 25 cm", desc);
                Assert.Contains("walnut wood", desc, StringComparison.OrdinalIgnoreCase);
            }
            if (mock.Description.Contains("King height 6.5 cm"))
            {
                Assert.Contains("King height 6.5 cm", desc);
                Assert.Contains("velvet base", desc);
            }
            if (mock.Description.Contains("35 cm - 45 cm"))
            {
                Assert.Contains("35 cm - 45 cm", desc);
                Assert.Contains("brass nameplate", desc, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void FullTikAi_Tags_AreDiverseAcrossProducts_AndNotIdentical()
    {
        // "Aynı tag oluşturuyor mu?" kontrolü
        var tagSets = new Dictionary<string, HashSet<string>>();

        foreach (var mock in DiverseSampleListings)
        {
            var input = new ListingOptimizationInput(
                mock.Title,
                mock.Description,
                mock.Tags,
                mock.TargetKeyword
            );
            var result = _optimizer.Optimize(input);
            tagSets[mock.Title] = result.TagSuggestions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // Farklı ürünlerin etiketleri birbirinden tamamen farklı olmalı
        var titles = tagSets.Keys.ToList();
        for (int i = 0; i < titles.Count; i++)
        {
            for (int j = i + 1; j < titles.Count; j++)
            {
                var tags1 = tagSets[titles[i]];
                var tags2 = tagSets[titles[j]];
                var common = tags1.Intersect(tags2).Count();

                // 13 etiketten en fazla 1-2 tanesi ("gift" varyasyonu vb.) ortak olabilir, asla birbirine benzememeli!
                Assert.True(common <= 2,
                    $"{titles[i]} ile {titles[j]} arasında {common} adet ortak etiket bulundu! Etiketler ürün bazında özelleşmeli.");
            }
        }
    }

    [Fact]
    public void FullTikAi_Tags_NeverExceed20Characters_AndMaximizeCapacity()
    {
        // "Mümkün olduğunca 20 karakterli mi oluşturuyor?" kontrolü
        var allGeneratedTags = new List<string>();

        foreach (var mock in DiverseSampleListings)
        {
            var input = new ListingOptimizationInput(
                mock.Title,
                mock.Description,
                mock.Tags,
                mock.TargetKeyword
            );
            var result = _optimizer.Optimize(input);
            allGeneratedTags.AddRange(result.TagSuggestions);
        }

        Assert.NotEmpty(allGeneratedTags);

        // 1. KESİN KURAL: Hiçbir etiket 20 karakteri ASLA aşamaz (Etsy API 20 char hard limit)
        foreach (var tag in allGeneratedTags)
        {
            Assert.True(tag.Length <= 20, $"Etiket 20 karakter sınırını aştı: '{tag}' ({tag.Length} karakter)");
            Assert.True(tag.Length >= 4, $"Etiket çok kısa: '{tag}' ({tag.Length} karakter)");
        }

        // 2. KAPASİTE KULLANIMI: Etiketlerin ortalama uzunluğu yüksek olmalı (14-20 karakter aralığında)
        // Etsy SEO'sunda 2-3 kelimeli zengin long-tail etiketler arama hacmini maksimize eder.
        var avgLength = allGeneratedTags.Average(t => t.Length);
        Assert.True(avgLength >= 14.5, $"Etiket ortalama uzunluğu beklenenden düşük: {avgLength:F1} karakter (Hedef: >= 14.5)");

        // 3. Etiketlerin en az %40'ı 16-20 karakter aralığında olmalı (20 karaktere mümkün olduğunca yakınlık)
        var nearLimitCount = allGeneratedTags.Count(t => t.Length is >= 16 and <= 20);
        var nearLimitRatio = (double)nearLimitCount / allGeneratedTags.Count;
        Assert.True(nearLimitRatio >= 0.40,
            $"20 karaktere yakın zengin etiket oranı: {nearLimitRatio:P1} (Hedef: >= %40)");
    }

    [Fact]
    public void FullTikAi_SeoQuality_NoKeywordStuffing_NoRawTagDumps_NoPromptLeaks()
    {
        // "SEO dolandırıcılığı yapıyor mu bunları kontrol et"
        foreach (var mock in DiverseSampleListings)
        {
            var input = new ListingOptimizationInput(
                mock.Title,
                mock.Description,
                mock.Tags,
                mock.TargetKeyword
            );
            var result = _optimizer.Optimize(input);

            // 1. Keyword stuffing kontrolü (Başlıkta hiçbir kelime 2'den fazla tekrar etmemeli)
            foreach (var title in result.TitleSuggestions)
            {
                var words = title.Split([' ', '-', ',', '|'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 3)
                    .GroupBy(w => w, StringComparer.OrdinalIgnoreCase);

                foreach (var g in words)
                {
                    Assert.True(g.Count() <= 2,
                        $"Başlıkta anahtar kelime doldurma (keyword stuffing) tespit edildi: '{g.Key}' {g.Count()} kez geçti.");
                }
            }

            // 2. Açıklamaya ham etiket yığma yasağı ("Tags: ..." spami bulunmamalı)
            Assert.DoesNotContain("Tags:", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Keywords:", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);

            // 3. Bot komut ve prompt sızıntısı bulunmamalı
            Assert.DoesNotContain("OUTPUT LANGUAGE", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Competitor description", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Selected marketplace listing", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FullTikAi_DetectsTrademarkRisks_ToProtectSellerFromSuspension()
    {
        // Çekilen rakip üründe Disney / Marvel / Spiderman gibi lisanslı terimler varsa satıcı uyarılmalı
        var trademarkInput = new ListingOptimizationInput(
            Title: "Marvel Spiderman 3D Printed Web Slinger Bust Statue",
            Description: "Action figure of Peter Parker Spiderman Disney hero.",
            Tags: ["spiderman", "marvel bust"],
            TargetKeyword: "spiderman statue"
        );

        var result = _optimizer.Optimize(trademarkInput);

        Assert.NotEmpty(result.RiskWarnings);
        Assert.Contains(result.RiskWarnings, w => w.Contains("marvel", StringComparison.OrdinalIgnoreCase) ||
                                                 w.Contains("spiderman", StringComparison.OrdinalIgnoreCase) ||
                                                 w.Contains("disney", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https://www.etsy.com/listing/1789552748/handmade-ceramic-coffee-mug", 1789552748, true)]
    [InlineData("https://www.etsy.com/listing/189234567/astronaut-lamp?click_key=abc123xyz&click_sum=987654", 189234567, true)]
    [InlineData("https://www.etsy.com/uk/listing/162345678/leather-wallet?ref=cart", 162345678, true)]
    [InlineData("https://www.etsy.com/your/shops/me/tools/listings/copy/1456789012", 1456789012, true)]
    [InlineData("https://openapi.etsy.com/v3/application/listings?listing_id=123456789", 123456789, true)]
    [InlineData("1789552748", 1789552748, true)]
    [InlineData("   https://www.etsy.com/listing/1789552748/mug   ", 1789552748, true)]
    [InlineData("https://www.etsy.com/shop/MyAwesomeShop", 0, false)]
    [InlineData("random-non-etsy-link", 0, false)]
    [InlineData("", 0, false)]
    public void LinktenAl_ExtractsListingId_FromVariousEtsyLinkFormats(string inputUrl, long expectedId, bool expectedSuccess)
    {
        // "detaylı linkten al tarafı çalışıyor mu etsyden bir ürün linkten almayı dene" kontrolü
        var success = EtsyListingUrlParser.TryExtractListingId(inputUrl, out var extractedId);

        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedId, extractedId);
            var canonicalUrl = EtsyListingUrlParser.BuildListingUrl(extractedId);
            Assert.Equal($"https://www.etsy.com/listing/{expectedId}", canonicalUrl);
        }
    }

    [Fact]
    public void LinktenAl_EndToEnd_FromListingLinkToFullTikDraft_GeneratesCompleteUniqueListing()
    {
        // 1. Kullanıcı "Linkten Al" kutusuna gerçekçi bir Etsy ürün linki yapıştırır
        const string pastedEtsyLink = "https://www.etsy.com/listing/1789552748/handmade-speckled-ceramic-coffee-mug-12oz?ref=shop_home_active_1";
        
        var parsed = EtsyListingUrlParser.TryExtractListingId(pastedEtsyLink, out var listingId);
        Assert.True(parsed);
        Assert.Equal(1789552748, listingId);

        // 2. Etsy API'den çekilen ürün verisi (İzole Mock nesne - Mağazanın gerçek listinglerine ASLA dokunmaz)
        var pulledProduct = new
        {
            ListingId = listingId,
            Title = "Handmade Speckled Ceramic Coffee Mug 12oz Artisanal Clay Cup",
            Description = "Artisan stoneware mug crafted for everyday coffee rituals.\nCapacity: 12 oz (350 ml)\nDimensions: Height 9.5 cm, diameter 8.5 cm\nMaterials: Stoneware clay, ceramic food-safe glaze\nDishwasher and microwave safe.\nPackage: 1x Handcrafted Mug, Care Guide Card.",
            Tags = new[] { "coffee mug", "ceramic mug", "tea cup", "clay mug", "pottery gift" },
            Materials = new[] { "Ceramic", "Clay", "Food Safe Glaze" },
            TargetKeyword = "ceramic coffee mug"
        };

        // 3. "1-Tık Full AI" (Full Tık AI) motoru tetiklenir
        var input = new ListingOptimizationInput(
            pulledProduct.Title,
            pulledProduct.Description,
            pulledProduct.Tags,
            pulledProduct.TargetKeyword
        );

        var result = _optimizer.Optimize(input);

        // Doğrulamalar:
        // A) Başlık kontrolleri
        Assert.NotEmpty(result.TitleSuggestions);
        var chosenTitle = result.TitleSuggestions.First();
        Assert.True(chosenTitle.Length <= 140, "Başlık Etsy 140 karakter sınırını aşmamalı");
        Assert.Contains("Ceramic Coffee Mug", chosenTitle, StringComparison.OrdinalIgnoreCase);

        // B) Etiket kontrolleri (13 adet, hepsi 20 karakter veya daha az)
        Assert.True(result.TagSuggestions.Count >= 10);
        foreach (var tag in result.TagSuggestions)
        {
            Assert.True(tag.Length <= 20, $"Etiket 20 karakter sınırını aştı: {tag}");
            Assert.True(tag.Length >= 4, $"Etiket çok kısa: {tag}");
        }

        // C) Malzeme kontrolleri
        Assert.Contains(result.MaterialSuggestions, m => m.Contains("Ceramic", StringComparison.OrdinalIgnoreCase) ||
                                                        m.Contains("Clay", StringComparison.OrdinalIgnoreCase) ||
                                                        m.Contains("Stoneware", StringComparison.OrdinalIgnoreCase));

        // D) Açıklama ve Sadakat kontrolleri (Boyutlar, kapasite, bakım korundu mu?)
        Assert.Contains("12 oz", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("9.5 cm", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dishwasher", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);

        // E) SEO dolandırıcılığı ve bot sızıntısı yokluğu
        Assert.DoesNotContain("Tags:", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OUTPUT LANGUAGE", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Competitor description", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FullTikAi_GenerateAndPrintSampleOutputs()
    {
        // Kullanıcının talep ettiği örnek ürün çıktılarını üreten ve raporlayan test
        foreach (var mock in DiverseSampleListings.Take(4))
        {
            var input = new ListingOptimizationInput(
                mock.Title,
                mock.Description,
                mock.Tags,
                mock.TargetKeyword
            );

            var result = _optimizer.Optimize(input);

            _output?.WriteLine($"================================================================================");
            _output?.WriteLine($"ÜRÜN: {mock.Title}");
            _output?.WriteLine($"HEDEF KELİME: {mock.TargetKeyword}");
            _output?.WriteLine($"ÖNERİLEN BAŞLIK ({result.TitleSuggestions.First().Length} karakter): {result.TitleSuggestions.First()}");
            _output?.WriteLine($"ÖNERİLEN MALZEMELER: {string.Join(", ", result.MaterialSuggestions)}");
            _output?.WriteLine($"ÖNERİLEN 13 ETİKET: {string.Join(", ", result.TagSuggestions.Take(13).Select(t => $"'{t}' ({t.Length}k)"))}");
            _output?.WriteLine($"AÇIKLAMA TASLAĞI:\n{result.DescriptionDraft}");
            _output?.WriteLine($"================================================================================\n");
        }
    }
}

