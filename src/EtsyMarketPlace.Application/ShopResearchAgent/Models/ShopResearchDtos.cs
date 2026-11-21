namespace EtsyMarketPlace.Application.ShopResearchAgent.Models;

using System;
using System.Collections.Generic;
using System.Threading;
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
    ResearchScheduleCadence ScheduleCadence,
    decimal? DefaultShippingCostUsd = null,
    decimal? DefaultPackagingCostUsd = null,
    decimal? ReturnReserveRatePct = null
);

public enum ResearchAiMode
{
    Economic = 0,
    Balanced = 1,
    FullAi = 2
}

public sealed record RunShopResearchRequest(
    string? FocusHint = null,
    int MaxCandidates = 10,
    string? Model = null,
    string? ApiKey = null,
    ResearchAiMode AiMode = ResearchAiMode.Economic
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

public sealed record ResearchFunnelTelemetry(
    int ScannedCount,
    int BlockedByIpCount,
    int RejectedByMemoryCount,
    int RejectedByPriceCount,
    int RejectedByMarginCount,
    int RejectedByDeadNicheCount,
    int RejectedByCannibalizationCount,
    int WinningCount,
    int CriticIterations,
    int EtsyCallsUsed,
    int EtsyCallBudget,
    string? ActiveSeasonName
);

public sealed record VariationTierDto(
    string TierName,
    string Label,
    decimal PriceUsd,
    string ValueProp
);

public sealed record ProductBlueprintDto(
    string MobileTitle55,
    string FullTitle140,
    IReadOnlyList<string> LongTailTags,
    IReadOnlyList<VariationTierDto> VariationLadder,
    string DifferentiationAngle,
    string CompetitorGap,
    string AiMockupBrief,
    IReadOnlyList<string> RecommendedMaterials
);

public sealed record ResearchProgressEvent(
    Guid RunId,
    string StageKey,
    string StageLabel,
    int StageIndex,
    int TotalStages,
    int ScannedCount,
    int RemainingCount,
    string Detail,
    DateTime TimestampUtc
);

public sealed class RequestBudget
{
    private int _used;

    public RequestBudget(int maxEtsyCalls = 16)
    {
        MaxEtsyCalls = Math.Max(1, maxEtsyCalls);
    }

    public int MaxEtsyCalls { get; }
    public int Used => Volatile.Read(ref _used);
    public int Remaining => Math.Max(0, MaxEtsyCalls - Used);

    public bool TryConsume()
    {
        while (true)
        {
            var current = Volatile.Read(ref _used);
            if (current >= MaxEtsyCalls)
                return false;

            if (Interlocked.CompareExchange(ref _used, current + 1, current) == current)
                return true;
        }
    }
}
