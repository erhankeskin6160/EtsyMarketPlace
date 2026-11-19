namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;

public sealed record EconomicsSimulationResult(
    decimal SellPriceUsd,
    decimal EffectiveFeeRatePct,
    decimal NetProfitStandardUsd,
    decimal NetMarginStandardPct,
    decimal NetProfitOffsiteAds12Usd,
    decimal NetProfitOffsiteAds15Usd,
    decimal EffectiveCogsUsd,
    string CalibrationSource);

public sealed record LandedEconomicsResult(
    decimal SellPriceUsd,
    decimal EffectiveFeeRatePct,
    decimal EffectiveCogsUsd,
    decimal EstimatedShippingUsd,
    decimal PackagingUsd,
    decimal ReturnReserveUsd,
    decimal NetProfitStandardUsd,
    decimal NetMarginStandardPct,
    decimal NetProfitLandedUsd,
    decimal NetMarginLandedPct,
    decimal NetProfitOffsiteAds12Usd,
    decimal NetProfitOffsiteAds15Usd,
    string CalibrationSource);

/// <summary>
/// Simulates realistic Etsy unit economics across Standard (0% Offsite Ads), 12% Offsite Ads, and 15% Offsite Ads scenarios,
/// plus TR->US/EU international landed unit economics (shipping, packaging, return/defect reserve).
/// Calibrates effective platform fee rate from the shop's live Etsy ledger when available.
/// </summary>
public sealed class ShopEconomicsSimulatorService
{
    // Realistic TR/International baseline Etsy stack:
    // 6.5% transaction + 3.0% payment + 2.5% currency + 1.1% TR regulatory operating fee = 13.1% + $0.45 fixed ($0.25 payment + $0.20 listing)
    private const decimal DefaultVariableFeeRate = 0.131m;
    private const decimal FixedFeePerOrderUsd = 0.45m;
    private const decimal Default3DFailureBufferMultiplier = 1.12m; // 12% print failure & post-processing buffer

    private readonly IEtsyLedgerReportService? _ledgerReportService;

    public ShopEconomicsSimulatorService(IEtsyLedgerReportService? ledgerReportService = null)
    {
        _ledgerReportService = ledgerReportService;
    }

    public async Task<(decimal VariableFeeRate, string Source)> ResolveShopVariableFeeRateAsync(
        string shopId,
        CancellationToken ct = default)
    {
        if (_ledgerReportService != null && !string.IsNullOrWhiteSpace(shopId))
        {
            try
            {
                var end = DateTimeOffset.UtcNow;
                var start = end.AddDays(-90);
                var ledger = await _ledgerReportService.GetLiveReportAsync(shopId, start, end, ct);
                if (ledger.LedgerOk && ledger.Performance != null && ledger.Performance.GrossSales > 100m)
                {
                    var feeRatio = Math.Abs(ledger.Performance.PlatformFees) / ledger.Performance.GrossSales;
                    if (feeRatio >= 0.08m && feeRatio <= 0.35m)
                    {
                        return (Math.Round(feeRatio, 4), "Canlı Etsy Ödeme Defteri (Son 90 Gün)");
                    }
                }
            }
            catch
            {
                // Fallback to realistic baseline if ledger is unavailable
            }
        }

        return (DefaultVariableFeeRate, "Etsy TR/US Standart Kesinti Modeli (%13.1 + $0.45)");
    }

    public EconomicsSimulationResult Simulate(
        decimal sellPriceUsd,
        decimal cogsRatio,
        bool is3DPrinted,
        decimal variableFeeRate,
        string calibrationSource)
    {
        if (sellPriceUsd <= 0m)
        {
            return new EconomicsSimulationResult(0m, 0m, 0m, 0m, 0m, 0m, 0m, calibrationSource);
        }

        var clampedCogs = Math.Clamp(cogsRatio, 0.05m, 0.85m);
        var rawCogs = sellPriceUsd * clampedCogs;
        var effectiveCogs = is3DPrinted ? Math.Round(rawCogs * Default3DFailureBufferMultiplier, 2) : Math.Round(rawCogs, 2);

        var clampedFeeRate = Math.Clamp(variableFeeRate, 0.08m, 0.35m);
        var standardFees = (sellPriceUsd * clampedFeeRate) + FixedFeePerOrderUsd;

        var netStandard = Math.Round(sellPriceUsd - standardFees - effectiveCogs, 2);
        var netMarginPct = Math.Round((netStandard / sellPriceUsd) * 100m, 1);

        var netOffsite12 = Math.Round(netStandard - (sellPriceUsd * 0.12m), 2);
        var netOffsite15 = Math.Round(netStandard - (sellPriceUsd * 0.15m), 2);
        var effectiveFeePct = Math.Round((standardFees / sellPriceUsd) * 100m, 1);

        return new EconomicsSimulationResult(
            Math.Round(sellPriceUsd, 2),
            effectiveFeePct,
            netStandard,
            netMarginPct,
            netOffsite12,
            netOffsite15,
            effectiveCogs,
            calibrationSource);
    }

    public static decimal EstimateInternationalShippingUsd(decimal sellPriceUsd, bool is3DPrinted)
    {
        if (sellPriceUsd <= 0m) return 0m;
        if (sellPriceUsd < 25m) return is3DPrinted ? 9.00m : 8.50m;
        if (sellPriceUsd <= 55m) return is3DPrinted ? 10.50m : 9.50m;
        return is3DPrinted ? 14.50m : 13.50m;
    }

    public LandedEconomicsResult SimulateLanded(
        decimal sellPriceUsd,
        decimal cogsRatio,
        bool is3DPrinted,
        decimal variableFeeRate,
        string calibrationSource,
        decimal configuredShippingUsd = 0m,
        decimal packagingUsd = 1.00m,
        decimal returnReserveRatePct = 4.0m)
    {
        if (sellPriceUsd <= 0m)
        {
            return new LandedEconomicsResult(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, calibrationSource);
        }

        var baseSim = Simulate(sellPriceUsd, cogsRatio, is3DPrinted, variableFeeRate, calibrationSource);
        var shippingUsd = configuredShippingUsd > 0m
            ? Math.Round(configuredShippingUsd, 2)
            : EstimateInternationalShippingUsd(sellPriceUsd, is3DPrinted);
        var cleanPack = Math.Max(0m, Math.Round(packagingUsd, 2));
        var clampedReturnPct = Math.Clamp(returnReserveRatePct, 0m, 20m);
        var returnReserveUsd = Math.Round((sellPriceUsd + shippingUsd + baseSim.EffectiveCogsUsd) * (clampedReturnPct / 100m), 2);

        var netLanded = Math.Round(baseSim.NetProfitStandardUsd - shippingUsd - cleanPack - returnReserveUsd, 2);
        var marginLandedPct = Math.Round((netLanded / sellPriceUsd) * 100m, 1);

        var activeBaseProfit = configuredShippingUsd > 0m ? netLanded : baseSim.NetProfitStandardUsd;
        var netOffsite12 = Math.Round(activeBaseProfit - (sellPriceUsd * 0.12m), 2);
        var netOffsite15 = Math.Round(activeBaseProfit - (sellPriceUsd * 0.15m), 2);

        return new LandedEconomicsResult(
            baseSim.SellPriceUsd,
            baseSim.EffectiveFeeRatePct,
            baseSim.EffectiveCogsUsd,
            shippingUsd,
            cleanPack,
            returnReserveUsd,
            baseSim.NetProfitStandardUsd,
            baseSim.NetMarginStandardPct,
            netLanded,
            marginLandedPct,
            netOffsite12,
            netOffsite15,
            calibrationSource);
    }
}
