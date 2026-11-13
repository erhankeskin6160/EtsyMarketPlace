using System;
using System.Collections.Generic;
using EtsyMarketPlace.Domain.Accounting.Entities;

namespace EtsyMarketPlace.Domain.Accounting;

/// <summary>
/// Genel Muhasebe Ünite 6 &amp; 7 esaslarına göre Normal ve Azalan Bakiyeler amortisman motoru.
/// </summary>
public static class DepreciationCalculator
{
    public sealed record YearlyScheduleRow(
        int YearNumber,
        DateTime YearStartUtc,
        DateTime YearEndExclusiveUtc,
        decimal OpeningNetBookValueTry,
        decimal AnnualDepreciationTry,
        decimal AccumulatedDepreciationEndTry,
        decimal ClosingNetBookValueTry);

    /// <summary>Bir sabit kıymetin ekonomik ömrü boyunca yıllık amortisman planını üretir.</summary>
    public static IReadOnlyList<YearlyScheduleRow> BuildSchedule(FixedAssetItem asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.UsefulLifeYears <= 0 || asset.AcquisitionCostTry <= 0m)
            return Array.Empty<YearlyScheduleRow>();

        var depreciableBase = Math.Max(0m, asset.AcquisitionCostTry - asset.SalvageValueTry);
        if (depreciableBase <= 0m)
            return Array.Empty<YearlyScheduleRow>();

        var startDate = DateTime.SpecifyKind(asset.AcquisitionDate.Date, DateTimeKind.Utc);
        var rows = new List<YearlyScheduleRow>(asset.UsefulLifeYears);
        decimal accumulated = 0m;
        decimal nbv = asset.AcquisitionCostTry;

        if (asset.Method == DepreciationMethod.StraightLine)
        {
            var standardAnnual = Math.Round(depreciableBase / asset.UsefulLifeYears, 2);
            for (var i = 0; i < asset.UsefulLifeYears; i++)
            {
                var yrStart = startDate.AddYears(i);
                var yrEnd = startDate.AddYears(i + 1);
                var remainingDepreciable = Math.Max(0m, depreciableBase - accumulated);
                var annual = i == asset.UsefulLifeYears - 1
                    ? Math.Round(remainingDepreciable, 2)
                    : Math.Min(standardAnnual, remainingDepreciable);

                accumulated = Math.Round(accumulated + annual, 2);
                var closingNbv = Math.Round(Math.Max(asset.SalvageValueTry, asset.AcquisitionCostTry - accumulated), 2);

                rows.Add(new YearlyScheduleRow(i + 1, yrStart, yrEnd, nbv, annual, accumulated, closingNbv));
                nbv = closingNbv;
            }
        }
        else
        {
            // Azalan Bakiyeler: Normal oranın iki katı, en fazla %50 (Ünite 6 & VUK Mük. 315)
            var rate = Math.Min(2m / asset.UsefulLifeYears, 0.50m);
            for (var i = 0; i < asset.UsefulLifeYears; i++)
            {
                var yrStart = startDate.AddYears(i);
                var yrEnd = startDate.AddYears(i + 1);
                var remainingDepreciable = Math.Max(0m, nbv - asset.SalvageValueTry);
                var annual = i == asset.UsefulLifeYears - 1
                    ? Math.Round(remainingDepreciable, 2)
                    : Math.Round(Math.Min(nbv * rate, remainingDepreciable), 2);

                accumulated = Math.Round(accumulated + annual, 2);
                var closingNbv = Math.Round(Math.Max(asset.SalvageValueTry, nbv - annual), 2);

                rows.Add(new YearlyScheduleRow(i + 1, yrStart, yrEnd, nbv, annual, accumulated, closingNbv));
                nbv = closingNbv;
            }
        }

        return rows;
    }

    /// <summary>Seçilen raporlama tarih aralığına isabet eden dönemsel amortisman payını hesaplar.</summary>
    public static FixedAssetDepreciationLine CalculateForPeriod(
        FixedAssetItem asset,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!asset.IsActive || asset.UsefulLifeYears <= 0 || asset.AcquisitionCostTry <= 0m)
        {
            return new FixedAssetDepreciationLine(
                asset.Id, asset.AssetName, asset.AssetAccountCode, asset.ExpenseTargetCategory,
                asset.Method, asset.AcquisitionCostTry, asset.UsefulLifeYears, 0m, 0m, asset.AcquisitionCostTry);
        }

        var schedule = BuildSchedule(asset);
        var pStart = periodStart.UtcDateTime.Date;
        var pEndExclusive = periodEnd.UtcDateTime.Date.AddDays(1);
        if (pEndExclusive <= pStart) pEndExclusive = pStart.AddDays(1);

        decimal periodDep = 0m;
        decimal accumulatedUpToEnd = 0m;

        foreach (var row in schedule)
        {
            var yearTotalDays = Math.Max(1m, (decimal)(row.YearEndExclusiveUtc - row.YearStartUtc).TotalDays);

            // Dönem içi amortisman kesişimi
            var overlapStart = pStart > row.YearStartUtc ? pStart : row.YearStartUtc;
            var overlapEnd = pEndExclusive < row.YearEndExclusiveUtc ? pEndExclusive : row.YearEndExclusiveUtc;
            if (overlapEnd > overlapStart)
            {
                var overlapDays = (decimal)(overlapEnd - overlapStart).TotalDays;
                periodDep += row.AnnualDepreciationTry * (overlapDays / yearTotalDays);
            }

            // Dönem sonu itibarıyla toplam birikmiş amortisman (257)
            if (pEndExclusive >= row.YearEndExclusiveUtc)
            {
                accumulatedUpToEnd += row.AnnualDepreciationTry;
            }
            else if (pEndExclusive > row.YearStartUtc)
            {
                var elapsedDays = (decimal)(pEndExclusive - row.YearStartUtc).TotalDays;
                accumulatedUpToEnd += row.AnnualDepreciationTry * (elapsedDays / yearTotalDays);
            }
        }

        var roundedPeriodDep = Math.Round(periodDep, 2);
        var roundedAccum = Math.Round(Math.Min(asset.AcquisitionCostTry - asset.SalvageValueTry, accumulatedUpToEnd), 2);
        var netBookValue = Math.Round(Math.Max(asset.SalvageValueTry, asset.AcquisitionCostTry - roundedAccum), 2);

        return new FixedAssetDepreciationLine(
            asset.Id,
            asset.AssetName,
            asset.AssetAccountCode,
            asset.ExpenseTargetCategory,
            asset.Method,
            Math.Round(asset.AcquisitionCostTry, 2),
            asset.UsefulLifeYears,
            roundedPeriodDep,
            roundedAccum,
            netBookValue);
    }
}

