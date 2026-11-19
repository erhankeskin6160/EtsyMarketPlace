namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using EtsyMarketPlace.Application.MarketResearch;
using EtsyMarketPlace.Domain.ShopResearchAgent;

public sealed record DemandValidationResult(
    int DemandScore,
    DemandConfidenceLevel Confidence,
    bool IsDeadNiche,
    int CompetitionAdvantage,
    string Summary,
    IReadOnlyList<string>? WarningFlags = null,
    int MonopolyPenalty = 0,
    int SeasonalBoostPct = 0,
    int MomentumScore = 50);

/// <summary>
/// Validates actual buyer demand independently of supply using velocity, favorites/views engagement,
/// bestseller density, monopoly concentration, free-shipping pressure, seasonal fit, and false-positive guards.
/// </summary>
public sealed class DemandValidationService
{
    public DemandValidationResult EvaluateMarketListing(
        MarketListingItemDto item,
        MarketSummaryKpisDto kpis,
        int seasonalBoostPct = 0,
        int keywordMomentumScore = 50)
    {
        var monthlySales = Math.Max(0, item.EstimatedMonthlySales);
        var favorites = Math.Max(0, item.Favorites);
        var reviews = Math.Max(0, item.ReviewCount);
        var views = Math.Max(1, item.Views);
        var totalListings = Math.Max(1, kpis.TotalListings);

        // 1. Sales & Review Velocity (0-40 pts)
        var velocityPts = Math.Min(40, (monthlySales * 1.6) + (reviews * 0.15));

        // 2. Engagement & Bestseller Signal (0-35 pts)
        var favRate = Math.Min(0.25, (double)favorites / views);
        var engagementPts = Math.Min(25, (favorites * 0.06) + (favRate * 60.0));
        if (item.IsBestseller) engagementPts += 10;
        engagementPts = Math.Min(35, engagementPts);

        // 3. Niche Market Momentum + V3 Seasonal & Keyword Delta Signals
        var bestsellerDensity = kpis.TotalListings > 0 ? (double)kpis.BestsellerCount / Math.Max(1, kpis.TotalListings) : 0;
        var nichePts = Math.Min(25, (kpis.AverageEstimatedMonthlySales * 1.2) + (bestsellerDensity * 30.0));

        var clampedMomentum = Math.Clamp(keywordMomentumScore, 0, 100);
        var momentumDeltaPts = (clampedMomentum - 50) * 0.12;
        var clampedSeasonPct = Math.Clamp(seasonalBoostPct, 0, 35);
        var seasonPts = clampedSeasonPct * 0.20;

        var demandScore = Math.Clamp(
            (int)Math.Round(velocityPts + engagementPts + nichePts + momentumDeltaPts + seasonPts),
            0, 100);

        // Competition Advantage (0-100) adjusted by monopoly & free-shipping pressure
        var baseCompAdv = totalListings switch
        {
            <= 100 => 90,
            <= 500 => 78,
            <= 2000 => 65,
            <= 10000 => 48,
            _ => 30
        };

        var monopolyPenalty = kpis.TopShopConcentrationPct >= 55 ? 22
            : kpis.TopShopConcentrationPct >= 40 ? 12
            : 0;
        var freeShippingPenalty = kpis.FreeShippingRatioPct >= 75 ? 6 : 0;
        var competitionAdvantage = Math.Clamp(baseCompAdv - monopolyPenalty - freeShippingPenalty, 5, 100);

        // False-positive traps (RedOcean, HighMonopoly, FreeShippingPressure, SeasonalPeak, LossLeader)
        var warnings = new List<string>();
        if (kpis.TopShopConcentrationPct >= 45)
            warnings.Add("HighMonopoly");
        if (kpis.FreeShippingRatioPct >= 70)
            warnings.Add("FreeShippingPressure");
        if (kpis.TotalListings > 5000 && kpis.AverageEstimatedMonthlySales < 3)
            warnings.Add("RedOcean");
        if (clampedSeasonPct >= 20)
            warnings.Add("SeasonalPeak");
        if (item.PriceUsd > 0m && kpis.MedianPrice > 0m && item.PriceUsd < kpis.MedianPrice * 0.65m)
            warnings.Add("LossLeader");

        // Dead niche detection: virtually no sales, very few favorites, and no bestseller traction
        var isDeadNiche = demandScore < 20 && monthlySales == 0 && favorites < 15 && !item.IsBestseller;

        var confidence = demandScore >= 65
            ? DemandConfidenceLevel.High
            : demandScore >= 38
                ? DemandConfidenceLevel.Medium
                : DemandConfidenceLevel.Low;

        var summary = isDeadNiche
            ? "Ölü niş sinyali: talep ve favori ivmesi çok zayıf."
            : $"Talep Skoru: {demandScore}/100 ({confidence}) | Aylık ~{monthlySales} satış, {favorites} favori, {reviews} yorum.";

        return new DemandValidationResult(
            demandScore,
            confidence,
            isDeadNiche,
            competitionAdvantage,
            summary,
            warnings,
            monopolyPenalty,
            clampedSeasonPct,
            clampedMomentum);
    }

    public DemandValidationResult Evaluate3DModel(int totalDownloads, int likesCount, int etsyCompetitionCount)
    {
        var downloadPts = Math.Min(50, totalDownloads / 40.0);
        var likePts = Math.Min(35, likesCount / 10.0);
        var demandScore = Math.Clamp((int)Math.Round(downloadPts + likePts + 15), 0, 100);

        var compAdv = etsyCompetitionCount switch
        {
            < 0 => 70,
            <= 10 => 92,
            <= 50 => 80,
            <= 200 => 62,
            _ => 40
        };

        var isDeadNiche = totalDownloads < 50 && likesCount < 10;
        var confidence = demandScore >= 65
            ? DemandConfidenceLevel.High
            : demandScore >= 40
                ? DemandConfidenceLevel.Medium
                : DemandConfidenceLevel.Low;

        return new DemandValidationResult(
            demandScore,
            confidence,
            isDeadNiche,
            compAdv,
            $"3D Talep Skoru: {demandScore}/100 ({totalDownloads} indirme, {likesCount} beğeni).",
            Array.Empty<string>(),
            0,
            0,
            Math.Clamp(demandScore, 35, 95));
    }
}
