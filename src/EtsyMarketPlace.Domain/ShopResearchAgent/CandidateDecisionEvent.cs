namespace EtsyMarketPlace.Domain.ShopResearchAgent;

using System;
using System.Collections.Generic;

/// <summary>
/// Append-only feedback event recording whether the store owner accepted or rejected a candidate.
/// Used to build positive/negative constraints in subsequent agent runs.
/// </summary>
public sealed class CandidateDecisionEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CandidateId { get; init; }
    public string OwnerUserId { get; init; } = string.Empty;
    public string ShopId { get; init; } = string.Empty;
    public string CandidateTitle { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public CandidateDecisionStatus Decision { get; init; }
    public CandidateRejectionReason Reason { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
