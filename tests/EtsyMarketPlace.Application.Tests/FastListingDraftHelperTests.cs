namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class FastListingDraftHelperTests
{
    [Theory]
    [InlineData("Boyut / Beden (Size - 100)", "Size", 100)]
    [InlineData("Size", "Size", 100)]
    [InlineData("Renk (Primary Color - 506)", "Primary color", 506)]
    [InlineData("Color", "Primary color", 506)]
    [InlineData("Malzeme (Material - 507)", "Material", 507)]
    [InlineData("Stil (Style - 514)", "Style", 514)]
    [InlineData("Özel Varyasyon (Custom - 513)", "Custom", 513)]
    [InlineData(null, "Custom", 513)]
    [InlineData("", "Custom", 513)]
    [InlineData("Random Text", "Custom", 513)]
    public void ParseVariationType_CorrectlyIdentifiesPropertyId(string? input, string expectedName, long expectedPropertyId)
    {
        var (name, propertyId) = FastListingDraftHelper.ParseVariationType(input);

        Assert.Equal(expectedName, name);
        Assert.Equal(expectedPropertyId, propertyId);
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
}
