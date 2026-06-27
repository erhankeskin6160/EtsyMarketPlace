namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ProductOpportunity;
using Xunit;

public sealed class ProductOpportunityScorerTests
{
    [Fact]
    public void Score_RewardsDemandAndSeoGap()
    {
        var scorer = new ProductOpportunityScorer();
        var score = scorer.Score(new ProductOpportunityInput(
            "Fantasy Wizard Bust Figurine",
            "Hand painted fantasy decor for collectors and gamer room display.",
            ["fantasy bust", "wizard decor", "gamer gift"],
            68,
            180,
            4200,
            850,
            55,
            2,
            "fantasy bust",
            "Art & Collectibles > Sculpture > Figurines"));

        Assert.True(score.Opportunity >= 50);
        Assert.True(score.Demand >= 50);
        Assert.True(score.SeoGap >= 40);
    }

    [Fact]
    public void Score_PenalizesTrademarkRisk()
    {
        var scorer = new ProductOpportunityScorer();
        var safeScore = scorer.Score(new ProductOpportunityInput(
            "Fantasy Wizard Bust Figurine",
            "Generic fantasy decor for collectors.",
            ["fantasy bust", "wizard decor"],
            68,
            120,
            3000,
            500,
            72,
            2,
            "fantasy bust",
            "Art & Collectibles"));
        var riskyScore = scorer.Score(new ProductOpportunityInput(
            "Gandalf Lord of the Rings LOTR Bust",
            "Gandalf LOTR replica collectible.",
            ["gandalf", "lotr", "lord of the rings"],
            68,
            120,
            3000,
            500,
            72,
            2,
            "gandalf bust",
            "Art & Collectibles"));

        Assert.True(riskyScore.Risk > safeScore.Risk);
        Assert.True(riskyScore.Opportunity < safeScore.Opportunity);
    }
}
