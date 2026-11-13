namespace EtsyMarketPlace.Application.Tests.ListingOptimization;

using System.Linq;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class EtsyTagNormalizerTests
{
    [Fact]
    public void NormalizeTo13Tags_ShouldPreserveValidTagsAndSwapOverLimitUsingBackupCandidates()
    {
        var candidates = new[]
        {
            "ben 10 cosplay prop",       // 19 - valid
            "collector display piece",   // 23 - over limit!
            "3d printed omnitrix",       // 19 - valid
            "ben tennyson gift",         // 17 - valid
            "cartoon cosplay prop",      // 20 - valid
            "kids costume watch",        // 18 - valid
            "sci fi fan gift",           // 15 - valid
            "cosplay wrist prop",        // 18 - valid
            "ben 10 fan decor",          // 16 - valid
            "durable pla prop",          // 16 - valid
            "birthday fan gift",         // 17 - valid
            "omnitrix watch rep",        // 18 - valid
            "alien watch prop",          // 16 - valid
            "hero watch replica"         // 18 - backup #14!
        };

        var result = EtsyTagNormalizer.NormalizeTo13Tags(candidates);

        Assert.Equal(13, result.Count);
        Assert.All(result, tag => Assert.True(tag.Length <= 20, $"Tag '{tag}' exceeded 20 chars"));
        Assert.DoesNotContain("collector display piece", result);
        Assert.Contains("hero watch replica", result);
        Assert.Contains("ben 10 cosplay prop", result);
    }

    [Fact]
    public void CompactAtWordBoundary_ShouldNeverTruncateMidWord()
    {
        // "collector display piece" (23 chars) -> "collector display" (17 chars)
        var compacted1 = EtsyTagNormalizer.CompactAtWordBoundary("collector display piece");
        Assert.Equal("collector display", compacted1);

        // Filler removal: "gift for anime fans club" (24 chars) -> "gift anime fans club" (20 chars)
        var compacted2 = EtsyTagNormalizer.CompactAtWordBoundary("gift for anime fans club");
        Assert.Equal("gift anime fans club", compacted2);
    }

    [Fact]
    public void NormalizeTo13Tags_ShouldFillTo13UsingFallbackWhenNeeded()
    {
        var aiTags = new[] { "ben 10 watch", "omnitrix prop" };
        var fallback = Enumerable.Range(1, 15).Select(i => $"fallback tag {i}").ToList();

        var result = EtsyTagNormalizer.NormalizeTo13Tags(aiTags, fallback);

        Assert.Equal(13, result.Count);
        Assert.All(result, t => Assert.True(t.Length <= 20));
        Assert.Contains("ben 10 watch", result);
    }
}
