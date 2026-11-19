namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ShopResearchAgent.Interfaces;
using EtsyMarketPlace.Domain.ShopResearchAgent;

/// <summary>
/// Beta-Bernoulli Thompson Sampling service with weekly exponential forgetting factor (gamma = 0.95)
/// driven by seller Accept / SendToDraft / Reject decisions.
/// </summary>
public sealed class BanditLearningService
{
    private const double WeeklyDecayRate = 0.95;
    private const double UnseenExplorationBonus = 0.12;

    private readonly IShopResearchRepository _repository;

    public BanditLearningService(IShopResearchRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task RecordOutcomeAsync(
        string ownerUserId,
        string shopId,
        string armKey,
        CandidateDecisionStatus decision,
        CancellationToken ct = default,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId) || string.IsNullOrWhiteSpace(shopId) || string.IsNullOrWhiteSpace(armKey))
            return;

        if (decision is not (CandidateDecisionStatus.Accepted or CandidateDecisionStatus.SentToDraft or CandidateDecisionStatus.Rejected))
            return;

        var normalizedKey = NormalizeArmKey(armKey);
        var now = utcNow ?? DateTime.UtcNow;
        var arms = await _repository.GetBanditArmsAsync(ownerUserId, shopId, ct);
        var existing = arms.FirstOrDefault(a => string.Equals(a.ArmKey, normalizedKey, StringComparison.OrdinalIgnoreCase));

        double alpha = 1.0;
        double beta = 1.0;
        int trials = 0;

        if (existing != null)
        {
            var elapsedDays = Math.Max(0.0, (now - existing.UpdatedAtUtc).TotalDays);
            var gamma = Math.Pow(WeeklyDecayRate, elapsedDays / 7.0);
            alpha = Math.Max(1.0, existing.Alpha * gamma);
            beta = Math.Max(1.0, existing.Beta * gamma);
            trials = existing.TotalTrials;
        }

        switch (decision)
        {
            case CandidateDecisionStatus.Accepted:
                alpha += 1.0;
                break;
            case CandidateDecisionStatus.SentToDraft:
                alpha += 0.3;
                break;
            case CandidateDecisionStatus.Rejected:
                beta += 1.0;
                break;
        }

        await _repository.UpsertBanditArmAsync(new BanditArmState
        {
            OwnerUserId = ownerUserId,
            ShopId = shopId,
            ArmKey = normalizedKey,
            Alpha = Math.Round(alpha, 4),
            Beta = Math.Round(beta, 4),
            TotalTrials = trials + 1,
            UpdatedAtUtc = now
        }, ct);
    }

    public IReadOnlyList<string> RankSeeds(
        IReadOnlyList<string> seeds,
        IReadOnlyList<BanditArmState> arms,
        Random? rng = null)
    {
        if (seeds == null || seeds.Count == 0)
            return Array.Empty<string>();

        var random = rng ?? Random.Shared;
        var armMap = (arms ?? Array.Empty<BanditArmState>())
            .GroupBy(a => NormalizeArmKey(a.ArmKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var scored = new List<(string Seed, double Score)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in seeds)
        {
            var clean = raw?.Trim();
            if (string.IsNullOrWhiteSpace(clean) || !seen.Add(clean))
                continue;

            var key = NormalizeArmKey(clean);
            double score;
            if (armMap.TryGetValue(key, out var arm) && arm.TotalTrials > 0)
            {
                var sampled = SampleBeta(arm.Alpha, arm.Beta, random);
                var mean = arm.Alpha / Math.Max(1.0, arm.Alpha + arm.Beta);
                score = (0.65 * mean) + (0.35 * sampled);
            }
            else
            {
                score = SampleBeta(1.0, 1.0, random) * 0.35 + 0.50 + UnseenExplorationBonus;
            }

            scored.Add((clean, score));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .Select(x => x.Seed)
            .ToList();
    }

    public static string NormalizeArmKey(string armKey) => (armKey ?? string.Empty).Trim().ToLowerInvariant();

    private static double SampleBeta(double alpha, double beta, Random rng)
    {
        var g1 = SampleGamma(Math.Max(1.0, alpha), rng);
        var g2 = SampleGamma(Math.Max(1.0, beta), rng);
        var sum = g1 + g2;
        return sum <= 0.0 ? 0.5 : Math.Clamp(g1 / sum, 0.0, 1.0);
    }

    // Marsaglia-Tsang method for Gamma(shape >= 1, scale = 1)
    private static double SampleGamma(double shape, Random rng)
    {
        var d = shape - (1.0 / 3.0);
        var c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double x;
            double v;
            do
            {
                x = SampleStandardNormal(rng);
                v = 1.0 + c * x;
            } while (v <= 0.0);

            v = v * v * v;
            var u = rng.NextDouble();
            if (u < 1.0 - 0.0331 * (x * x) * (x * x))
                return d * v;
            if (Math.Log(Math.Max(u, 1e-12)) < 0.5 * x * x + d * (1.0 - v + Math.Log(v)))
                return d * v;
        }
    }

    private static double SampleStandardNormal(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
