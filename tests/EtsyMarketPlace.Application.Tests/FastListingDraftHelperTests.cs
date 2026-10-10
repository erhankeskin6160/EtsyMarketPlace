namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class FastListingDraftHelperTests
{
    [Theory]
    [InlineData("Boyut / Beden (Size)", "Size", 513)]
    [InlineData("Size", "Size", 513)]
    [InlineData("Renk (Color)", "Color", 513)]
    [InlineData("Color", "Color", 513)]
    [InlineData("Malzeme (Material)", "Material", 513)]
    [InlineData("Stil (Style)", "Style", 513)]
    [InlineData("Özel Varyasyon (Custom)", "Custom", 513)]
    [InlineData(null, "Custom", 513)]
    [InlineData("", "Custom", 513)]
    [InlineData("Random Text", "Custom", 513)]
    public void ParseVariationType_CorrectlyIdentifiesPropertyId(string? input, string expectedName, long expectedPropertyId)
    {
        var (name, propertyId) = FastListingDraftHelper.ParseVariationType(input, 1);

        Assert.Equal(expectedName, name);
        Assert.Equal(expectedPropertyId, propertyId);
    }

    [Fact]
    public void ParseVariationType_GroupIndex2_UsesPropertyId514()
    {
        var (name, propertyId) = FastListingDraftHelper.ParseVariationType("Color", 2);

        Assert.Equal("Color", name);
        Assert.Equal(514, propertyId);
    }

    [Fact]
    public void SanitizeTags_SplitsCommasAndNewlines_RemovesDuplicatesAndEnforcesLimits()
    {
        var rawTags = "hand made, handmade, Hand Made, vintage, very long tag that exceeds twenty chars, cozy gift, gift for her";

        var sanitized = FastListingDraftHelper.SanitizeTags(rawTags);

        // "hand made", "handmade", "Hand Made" -> "hand made" and "handmade" (duplicates removed case-insensitively)
        Assert.Contains("hand made", sanitized);
        Assert.Contains("handmade", sanitized);
        Assert.DoesNotContain("Hand Made", sanitized); // Case-insensitive duplicate filtered

        // Length limit check: "very long tag that exceeds twenty chars" is > 20 chars, truncated to 20
        var truncatedTag = sanitized.FirstOrDefault(t => t.StartsWith("very long"));
        Assert.NotNull(truncatedTag);
        Assert.True(truncatedTag.Length <= 20);

        // Total count should not exceed 13
        Assert.True(sanitized.Count <= 13);
    }

    [Fact]
    public void SanitizeTags_LimitsTo13TagsMaximum()
    {
        var rawTags = string.Join(",", Enumerable.Range(1, 20).Select(i => $"tag{i}"));

        var result = FastListingDraftHelper.SanitizeTags(rawTags);

        Assert.Equal(13, result.Count);
        Assert.Equal("tag1", result[0]);
        Assert.Equal("tag13", result[12]);
    }

    [Fact]
    public void ValidateListing_ReturnsExpectedErrors_WhenFieldsAreInvalid()
    {
        var errors = FastListingDraftHelper.ValidateListing(
            title: "",
            price: 0,
            quantity: 0,
            imagePaths: []);

        Assert.Contains(errors, e => e.Contains("başlığı zorunludur"));
        Assert.Contains(errors, e => e.Contains("Fiyat 0'dan büyük"));
        Assert.Contains(errors, e => e.Contains("Stok adedi"));
        Assert.Contains(errors, e => e.Contains("görsel"));
    }

    [Fact]
    public void ValidateListing_ReturnsNoErrors_WhenAllValid()
    {
        var errors = FastListingDraftHelper.ValidateListing(
            title: "Modern Minimalist Ceramic Coffee Mug 12oz",
            price: 24.99m,
            quantity: 10,
            imagePaths: ["C:\\mock\\image1.jpg"]);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateListing_RejectsTitleOver140Characters()
    {
        var longTitle = new string('A', 141);
        var errors = FastListingDraftHelper.ValidateListing(
            title: longTitle,
            price: 25.00m,
            quantity: 5,
            imagePaths: ["C:\\mock\\image.jpg"]);

        Assert.Single(errors);
        Assert.Contains("140 karakter", errors[0]);
    }

    [Theory]
    [InlineData(5, 10, 50, false)]
    [InlineData(8, 9, 72, true)] // 72 > 70 max allowed
    [InlineData(10, 0, 10, false)]
    [InlineData(0, 0, 0, false)]
    public void CalculateVariationCombinations_CalculatesAndEnforcesEtsyLimit(
        int group1Count,
        int group2Count,
        int expectedTotal,
        bool expectedExceedsLimit)
    {
        var (total, exceedsLimit) = FastListingDraftHelper.CalculateVariationCombinations(group1Count, group2Count);

        Assert.Equal(expectedTotal, total);
        Assert.Equal(expectedExceedsLimit, exceedsLimit);
    }

    [Fact]
    public void SanitizeMaterials_StripsInvalidCharactersAndTransliteratesTurkish()
    {
        var rawMaterials = new List<string>
        {
            "Resin / PLA & Acrylic Paint (UV-cured)",
            "Ahşap, Gümüş & Doğal Taş",
            "Handmade 100% Cotton & Linen!",
            "   ",
            "Resin / PLA & Acrylic Paint (UV-cured)" // duplicate
        };

        var sanitized = FastListingDraftHelper.SanitizeMaterials(rawMaterials);

        // Assert no forbidden characters exist in any item
        foreach (var mat in sanitized)
        {
            Assert.DoesNotContain("/", mat);
            Assert.DoesNotContain("&", mat);
            Assert.DoesNotContain("(", mat);
            Assert.DoesNotContain(")", mat);
            Assert.DoesNotContain("%", mat);
            Assert.DoesNotContain("!", mat);
            Assert.DoesNotContain(",", mat);
            Assert.True(mat.Length <= 45);
        }

        // Check Turkish transliteration
        Assert.Contains(sanitized, m => m.Contains("Ahsap Gumus Dogal Tas"));
        Assert.Contains(sanitized, m => m.Contains("Resin PLA Acrylic Paint UV-cured"));
        Assert.Contains(sanitized, m => m.Contains("Handmade 100 Cotton Linen"));

        // Duplicates removed
        Assert.Equal(3, sanitized.Count);
    }

    [Fact]
    public void SanitizeMaterials_LimitsTo13MaterialsAnd45Characters()
    {
        var veryLongMaterial = "This is a very long handcrafted material name that definitely exceeds forty five characters threshold completely";
        var list = new List<string> { veryLongMaterial };
        for (int i = 1; i <= 20; i++)
        {
            list.Add($"Material Number {i}");
        }

        var result = FastListingDraftHelper.SanitizeMaterials(list);

        Assert.Equal(13, result.Count);
        Assert.True(result[0].Length <= 45);
    }

    [Fact]
    public void SanitizeTags_EnumerableOverload_StripsInvalidSymbols()
    {
        var rawTags = new List<string>
        {
            "Gothic & Skull / Horror",
            "Anubis Figure (3D)",
            "3D Print PLA!"
        };

        var result = FastListingDraftHelper.SanitizeTags(rawTags);

        Assert.Equal(3, result.Count);
        foreach (var tag in result)
        {
            Assert.DoesNotContain("&", tag);
            Assert.DoesNotContain("/", tag);
            Assert.DoesNotContain("(", tag);
            Assert.DoesNotContain(")", tag);
            Assert.DoesNotContain("!", tag);
            Assert.True(tag.Length <= 20);
        }
    }
}
