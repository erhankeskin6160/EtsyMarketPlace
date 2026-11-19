namespace EtsyMarketPlace.Domain.ShopResearchAgent;

using System;

/// <summary>
/// Persists Beta-Bernoulli Thompson Sampling arm statistics per shop/niche seed.
/// </summary>
public sealed class BanditArmState
{
    public string OwnerUserId { get; init; } = string.Empty;
    public string ShopId { get; init; } = string.Empty;
    public string ArmKey { get; init; } = string.Empty;
    public double Alpha { get; set; } = 1.0;
    public double Beta { get; set; } = 1.0;
    public int TotalTrials { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
