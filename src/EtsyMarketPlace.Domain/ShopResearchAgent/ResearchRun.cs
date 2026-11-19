namespace EtsyMarketPlace.Domain.ShopResearchAgent;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a single execution of the personalized store product research agent.
/// </summary>
public sealed class ResearchRun
{
    public Guid RunId { get; init; } = Guid.NewGuid();
    public string OwnerUserId { get; init; } = string.Empty;
    public string ShopId { get; init; } = string.Empty;
    public ResearchTriggerType TriggerType { get; init; } = ResearchTriggerType.Manual;
    public ResearchRunStatus Status { get; set; } = ResearchRunStatus.Running;
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public IReadOnlyList<string> SearchQueriesUsed { get; set; } = Array.Empty<string>();
    public string? ExplorationQueryUsed { get; set; }
    public decimal EffectiveShopFeeRatePct { get; set; }
    public string HypothesesJson { get; set; } = "[]";
    public string FunnelTelemetryJson { get; set; } = "{}";
    public int CriticIterations { get; set; } = 1;
    public string Summary { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<ResearchCandidate> Candidates { get; set; } = Array.Empty<ResearchCandidate>();
}
