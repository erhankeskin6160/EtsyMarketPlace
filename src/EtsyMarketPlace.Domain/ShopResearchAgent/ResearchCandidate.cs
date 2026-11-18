namespace EtsyMarketPlace.Domain.ShopResearchAgent;

using System;
using System.Collections.Generic;

/// <summary>
/// A grounded, profit-verified product opportunity candidate discovered specifically for a shop.
/// </summary>
public sealed class ResearchCandidate
{
    public Guid CandidateId { get; init; } = Guid.NewGuid();
    public Guid RunId { get; init; }
    public string OwnerUserId { get; init; } = string.Empty;
    public string ShopId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;
    public string SuggestedTitle { get; init; } = string.Empty;
    public IReadOnlyList<string> SuggestedTags { get; init; } = Array.Empty<string>();
    public string SourcePlatform { get; init; } = "EtsyMarket";
    public string SourceUrl { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;

    public decimal EstimatedSellPriceUsd { get; init; }
    public decimal EstimatedNetProfitUsd { get; init; }
    public decimal EstimatedNetMarginPct { get; init; }
    public int CompetitionCount { get; init; }
    public double DemandSignalScore { get; init; }
    public int OpportunityScore { get; init; }
    public int RiskScore { get; init; }
    public int CannibalizationScore { get; init; }
    public string? CannibalizationWarning { get; init; }

    public string Rationale { get; init; } = string.Empty;
    public string EvidenceJson { get; init; } = "{}";

    public CandidateDecisionStatus DecisionStatus { get; set; } = CandidateDecisionStatus.Pending;
    public CandidateRejectionReason RejectionReason { get; set; } = CandidateRejectionReason.None;
    public DateTime? DecidedAtUtc { get; set; }
}
