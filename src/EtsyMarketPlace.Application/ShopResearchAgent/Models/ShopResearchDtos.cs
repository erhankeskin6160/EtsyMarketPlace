namespace EtsyMarketPlace.Application.ShopResearchAgent.Models;

using System;
using System.Collections.Generic;
using EtsyMarketPlace.Domain.ShopResearchAgent;

public sealed record UpdateShopDnaRequest(
    string PrimaryNiche,
    string TargetAudience,
    decimal TargetPriceMinUsd,
    decimal TargetPriceMaxUsd,
    decimal MinAcceptableNetMarginPct,
    decimal EstimatedCogsRatio,
    bool Supports3DPrint,
    IReadOnlyList<string> PreferredMaterials,
    IReadOnlyList<string> ForbiddenKeywords,
    ResearchScheduleCadence ScheduleCadence
);

public sealed record RunShopResearchRequest(
    string? FocusHint = null,
    int MaxCandidates = 10,
    string? Model = null,
    string? ApiKey = null
);

public sealed record RecordCandidateDecisionRequest(
    CandidateDecisionStatus Decision,
    CandidateRejectionReason Reason = CandidateRejectionReason.None
);

public sealed record CannibalizationCheckResult(
    int Score,
    bool IsBlocked,
    string? MatchedListingTitle,
    string? WarningMessage
);
