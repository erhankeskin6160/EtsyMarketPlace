namespace EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.ShopResearchAgent;

/// <summary>
/// Persistence contract for personalized shop DNA, research runs, candidates, and decision memory.
/// Every query/mutation is strictly scoped by ownerUserId and shopId.
/// </summary>
public interface IShopResearchRepository
{
    Task<ShopDnaProfile?> GetDnaProfileAsync(string ownerUserId, string shopId, CancellationToken ct = default);
    Task UpsertDnaProfileAsync(ShopDnaProfile profile, CancellationToken ct = default);
    Task<IReadOnlyList<ShopDnaProfile>> GetScheduledProfilesAsync(CancellationToken ct = default);
    Task UpdateLastScheduledRunAsync(string ownerUserId, string shopId, DateTime utcNow, CancellationToken ct = default);

    Task SaveRunAsync(ResearchRun run, CancellationToken ct = default);
    Task<IReadOnlyList<ResearchRun>> GetRunsAsync(string ownerUserId, string shopId, int limit = 20, CancellationToken ct = default);
    Task<ResearchRun?> GetRunByIdAsync(string ownerUserId, string shopId, Guid runId, CancellationToken ct = default);

    Task<ResearchCandidate?> GetCandidateByIdAsync(string ownerUserId, string shopId, Guid candidateId, CancellationToken ct = default);
    Task UpdateCandidateDecisionAsync(
        string ownerUserId,
        string shopId,
        Guid candidateId,
        CandidateDecisionStatus status,
        CandidateRejectionReason reason,
        CancellationToken ct = default);

    Task AppendDecisionEventAsync(CandidateDecisionEvent decisionEvent, CancellationToken ct = default);
    Task<IReadOnlyList<CandidateDecisionEvent>> GetRecentDecisionEventsAsync(string ownerUserId, string shopId, int limit = 25, CancellationToken ct = default);

    Task<IReadOnlyList<BanditArmState>> GetBanditArmsAsync(string ownerUserId, string shopId, CancellationToken ct = default);
    Task UpsertBanditArmAsync(BanditArmState arm, CancellationToken ct = default);
}
