namespace EtsyMarketPlace.Application.Viral3DModels.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Viral3DModels.Entities;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;
using EtsyMarketPlace.Domain.Viral3DModels.Interfaces;

public sealed class Viral3DModelHunterService
{
    private readonly List<I3DModelPlatformScraper> _scrapers;
    private readonly IEtsyCompetitionChecker? _competitionChecker;

    public Viral3DModelHunterService(
        IEnumerable<I3DModelPlatformScraper> scrapers,
        IEtsyCompetitionChecker? competitionChecker = null)
    {
        _scrapers = scrapers?.ToList() ?? [];
        _competitionChecker = competitionChecker;
    }

    /// <summary>
    /// Scans all active 3D printing platforms in parallel, filters and ranks models by opportunity score.
    /// </summary>
    public async Task<IReadOnlyList<Trending3DModel>> ScanTrendingModelsAsync(
        ModelPlatformType? platformFilter = null,
        bool commercialOnly = false,
        int minOpportunityScore = 0,
        CancellationToken ct = default)
    {
        var targetScrapers = platformFilter.HasValue
            ? _scrapers.Where(s => s.PlatformType == platformFilter.Value).ToList()
            : _scrapers;

        var tasks = targetScrapers.Select(s => s.GetTrendingModelsAsync(1, ct));
        var results = await Task.WhenAll(tasks);

        var allModels = results.SelectMany(r => r).ToList();

        // Calculate scores & cross-check Etsy competition
        foreach (var model in allModels)
        {
            if (model.EtsyCompetitionCount < 0 && _competitionChecker != null)
            {
                model.EtsyCompetitionCount = await _competitionChecker.CheckEtsyCompetitionCountAsync(model.Title, ct);
            }
            model.OpportunityScore = CalculateOpportunityScore(model);
        }

        // Apply filters
        var query = allModels.AsEnumerable();

        if (commercialOnly)
        {
            query = query.Where(m => m.License.IsCommercialAllowed);
        }

        if (minOpportunityScore > 0)
        {
            query = query.Where(m => m.OpportunityScore >= minOpportunityScore);
        }

        return query.OrderByDescending(m => m.OpportunityScore)
                    .ThenByDescending(m => m.Downloads24h)
                    .ToList();
    }

    /// <summary>
    /// Mathematical Opportunity Score: High 3D velocity + low Etsy competition + commercial rights = Golden Opportunity.
    /// </summary>
    public static int CalculateOpportunityScore(Trending3DModel model)
    {
        double score = 50.0;

        // 1. Download & Print Velocity (up to +30 points)
        if (model.Downloads24h > 4000) score += 30;
        else if (model.Downloads24h > 2500) score += 24;
        else if (model.Downloads24h > 1500) score += 18;
        else if (model.Downloads24h > 800) score += 12;
        else score += 5;

        // 2. Verified Successful Prints Bonus (MakerWorld / Creality prints count)
        if (model.PrintsCount > 3000) score += 10;
        else if (model.PrintsCount > 1000) score += 6;

        // 3. Etsy Competition Arbitrage
        if (model.EtsyCompetitionCount == 0)
        {
            // Zero competitors on Etsy! True Blue Ocean
            score += 25;
        }
        else if (model.EtsyCompetitionCount is >= 1 and <= 3)
        {
            score += 18;
        }
        else if (model.EtsyCompetitionCount is >= 4 and <= 8)
        {
            score += 8;
        }
        else if (model.EtsyCompetitionCount > 8)
        {
            // High competition penalty
            score -= Math.Min(35, model.EtsyCompetitionCount * 2);
        }

        // 4. Commercial License multiplier
        if (model.License.IsCommercialAllowed)
        {
            score *= 1.05; // 5% bonus for certified commercial selling rights
        }
        else
        {
            score *= 0.35; // Severe penalty if non-commercial
        }

        return Math.Clamp((int)Math.Round(score), 5, 99);
    }
}
