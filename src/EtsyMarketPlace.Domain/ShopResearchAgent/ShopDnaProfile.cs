namespace EtsyMarketPlace.Domain.ShopResearchAgent;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the versioned DNA profile of a specific user's Etsy shop,
/// used to personalize autonomous product research and enforce store-level constraints.
/// </summary>
public sealed class ShopDnaProfile
{
    public string OwnerUserId { get; init; } = string.Empty;
    public string ShopId { get; init; } = string.Empty;
    public int DnaVersion { get; set; } = 1;
    public string PrimaryNiche { get; set; } = "Genel Etsy Mağazası";
    public string TargetAudience { get; set; } = string.Empty;
    public decimal TargetPriceMinUsd { get; set; } = 15m;
    public decimal TargetPriceMaxUsd { get; set; } = 75m;
    public decimal MinAcceptableNetMarginPct { get; set; } = 25m;
    public decimal EstimatedCogsRatio { get; set; } = 0.35m;
    public bool Supports3DPrint { get; set; }
    public IReadOnlyList<string> PreferredMaterials { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TopPerformingTags { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> CategoryKeywords { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ForbiddenKeywords { get; set; } = Array.Empty<string>();
    public ResearchScheduleCadence ScheduleCadence { get; set; } = ResearchScheduleCadence.Off;
    public DateTime? LastScheduledRunAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal DefaultShippingCostUsd { get; set; } = 0m;
    public decimal DefaultPackagingCostUsd { get; set; } = 1.00m;
    public decimal ReturnReserveRatePct { get; set; } = 4.0m;

    public bool IsScheduleDue(DateTime utcNow)
    {
        if (ScheduleCadence == ResearchScheduleCadence.Off)
            return false;

        if (!LastScheduledRunAtUtc.HasValue)
            return true;

        var elapsed = utcNow - LastScheduledRunAtUtc.Value;
        return ScheduleCadence switch
        {
            ResearchScheduleCadence.Daily => elapsed >= TimeSpan.FromHours(23),
            ResearchScheduleCadence.Weekly => elapsed >= TimeSpan.FromDays(7),
            ResearchScheduleCadence.Monthly => elapsed >= TimeSpan.FromDays(30),
            _ => false
        };
    }
}
