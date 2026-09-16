namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class AiCategorySuggesterTests
{
    [Fact]
    public void LocalCategoryHeuristics_IdentifiesHeadphoneStandCorrectly()
    {
        var title = "Smeagol Kulaklık Standı | Kulaklık Smeagol | Desk Decor";
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        Assert.NotNull(result);
        Assert.Equal(2079, result.TaxonomyId);
        Assert.Contains("Headphone", result.CategoryPath);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Fact]
    public void LocalCategoryHeuristics_Identifies3DPrintBustCorrectly()
    {
        var title = "Gollum 3D Printed Statue Bust | Lord of the Rings Figür";
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        Assert.NotNull(result);
        Assert.Equal(1239, result.TaxonomyId);
        Assert.Contains("Sculptures", result.CategoryPath);
    }

    [Fact]
    public void LocalCategoryHeuristics_IdentifiesMugCorrectly()
    {
        var title = "Custom Ceramic Coffee Mug | Fincan Kupa Hediye";
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        Assert.NotNull(result);
        Assert.Equal(943, result.TaxonomyId);
        Assert.Contains("Mugs", result.CategoryPath);
    }

    [Fact]
    public void LocalCategoryHeuristics_IdentifiesJewelryCorrectly()
    {
        var title = "Minimalist Silver Choker Necklace | Zarif Kolye";
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        Assert.NotNull(result);
        Assert.Equal(204, result.TaxonomyId);
        Assert.Contains("Necklaces", result.CategoryPath);
    }

    [Fact]
    public void LocalCategoryHeuristics_ReturnsFallbackOnEmptyTitle()
    {
        var result = LocalCategoryHeuristics.SuggestFromText("");

        Assert.NotNull(result);
        Assert.Equal(1239, result.TaxonomyId);
        Assert.True(result.ProviderUsed.Contains("Varsayılan") || result.ProviderUsed.Contains("Default"));
    }

    [Fact]
    public void LocalCategoryHeuristics_IdentifiesHandmadeWomensBag_Correctly()
    {
        var title = "El yapımı kadın çantası | Özel Tasarım Omuz Çantası";
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        Assert.NotNull(result);
        Assert.Equal(132, result.TaxonomyId);
        Assert.Contains("Shoulder Bags", result.CategoryPath);
        Assert.Contains("Yerel Kural Motoru", result.ProviderUsed);
    }

    [Fact]
    public void LocalCategoryHeuristics_IdentifiesToteBag_Correctly()
    {
        var title = "Organic Cotton Tote Bag | Bez Çanta Günlük Alışveriş Çantası";
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        Assert.NotNull(result);
        Assert.Equal(134, result.TaxonomyId);
        Assert.Contains("Tote Bags", result.CategoryPath);
        Assert.Contains("Yerel Kural Motoru", result.ProviderUsed);
    }

    [Fact]
    public void LocalCategoryHeuristics_IdentifiesBackpack_Correctly()
    {
        var title = "Deri Sırt Çantası | Vintage Travel Leather Backpack";
        var result = LocalCategoryHeuristics.SuggestFromText(title);

        Assert.NotNull(result);
        Assert.Equal(140, result.TaxonomyId);
        Assert.Contains("Backpacks", result.CategoryPath);
    }

    [Fact]
    public void ParseCategoryResponse_ExtractsValidJsonWithFencesAndAlternatives()
    {
        var raw = """
            ```json
            {
              "taxonomy_id": 2079,
              "category_path": "Electronics & Accessories > Audio > Headphone & Headset Stands",
              "confidence_score": 96,
              "reasoning": "Ürün açıkça bir kulaklık standı.",
              "alternatives": [
                {
                  "taxonomy_id": 1239,
                  "category_path": "Art & Collectibles > Sculptures > Busts & Statues",
                  "confidence_score": 85
                }
              ]
            }
            ```
            """;

        var result = CategoryResponseParser.Parse(raw, "OpenAI");

        Assert.NotNull(result);
        Assert.Equal(2079, result.TaxonomyId);
        Assert.Equal("Electronics & Accessories > Audio > Headphone & Headset Stands", result.CategoryPath);
        Assert.Equal(96, result.ConfidenceScore);
        Assert.Equal("OpenAI", result.ProviderUsed);
        Assert.Single(result.Alternatives);
        Assert.Equal(1239, result.Alternatives[0].TaxonomyId);
    }

    [Fact]
    public void ParseCategoryResponse_ExtractsJson_EvenWithDeepSeekThoughtStreamOrPretext()
    {
        var raw = """
            💭 [DeepSeek Düşünce Akışı]:
            Kullanıcı 'El yapımı kadın çantası' girdi. Etsy taksonomisinde en doğru kategori Bags & Purses > Handbags > Shoulder Bags olmalıdır.

            ```json
            {
              "taxonomy_id": 132,
              "category_path": "Bags & Purses > Handbags > Shoulder Bags",
              "confidence_score": 98,
              "reasoning": "Ürün el yapımı kadın omuz çantasıdır.",
              "alternatives": [
                {
                  "taxonomy_id": 134,
                  "category_path": "Bags & Purses > Handbags > Tote Bags",
                  "confidence_score": 85
                }
              ]
            }
            ```
            """;

        var result = CategoryResponseParser.Parse(raw, "DeepSeek (deepseek-reasoner)");

        Assert.NotNull(result);
        Assert.Equal(132, result.TaxonomyId);
        Assert.Equal("Bags & Purses > Handbags > Shoulder Bags", result.CategoryPath);
        Assert.Equal(98, result.ConfidenceScore);
        Assert.Equal("DeepSeek (deepseek-reasoner)", result.ProviderUsed);
        Assert.Single(result.Alternatives);
        Assert.Equal(134, result.Alternatives[0].TaxonomyId);
    }

    [Fact]
    public void ParseCategoryResponse_ReturnsNullOnMalformedJson()
    {
        var malformed = "Bu bir kategori önerisidir: taxonomy 12345 ama JSON değil.";
        var result = CategoryResponseParser.Parse(malformed, "Test");

        Assert.Null(result);
    }
}
