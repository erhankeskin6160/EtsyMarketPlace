using System;
using System.Collections.Generic;

namespace EtsyMarketPlace.Application.EtsyIntegration;

public sealed record FinancialAnalysisSnapshot(
    string ShopId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    DateTimeOffset GeneratedAt,
    FinancialPerformance Performance,
    IReadOnlyList<EtsyBankPayout> Payouts,
    IReadOnlyList<EtsyFinancialTransaction> Transactions,
    IReadOnlyList<EtsyOrderCostAlert> OrderAlerts);

public sealed record FinancialAnalysisResult(
    FinancialAnalysisSnapshot Snapshot,
    decimal RevenueChangePercent,
    decimal ExpenseRatioPercent,
    decimal ProfitMarginPercent,
    IReadOnlyList<FinancialAnalysisInsight> Insights,
    IReadOnlyList<FinancialAnalysisRecommendation> Recommendations);

public sealed record FinancialAnalysisInsight(
    string Severity,
    string Code,
    string Title,
    string Description,
    decimal? Value = null,
    string? Unit = null);

public sealed record FinancialAnalysisRecommendation(
    string Priority,
    string Title,
    string Description);

public sealed record AiTrafficFunnelSnapshotDto(
    int ActiveListingCount,
    int TotalViews,
    int EstimatedUniqueVisits,
    int TotalFavorites,
    int EstimatedAddToCarts,
    int RealizedOrders,
    decimal ViewToVisitRatePct,
    decimal FavoriteToViewRatePct,
    decimal AddToCartRatePct,
    decimal CartToOrderConversionPct,
    decimal OverallConversionRatePct,
    int ProjectedNext30dViews,
    int ProjectedNext30dVisits,
    int ProjectedNext30dFavorites,
    int ProjectedNext30dCarts,
    string FunnelBottleneckStage,
    string FunnelAiDiagnosis);

public sealed record AiForecastHorizonItemDto(
    string HorizonCode,
    string HorizonTitle,
    int Days,
    decimal ExpectedGrossUsd,
    decimal ExpectedGrossTry,
    decimal ExpectedFeesAndAdsUsd,
    decimal ExpectedCostsUsd,
    decimal ExpectedNetProfitUsd,
    decimal ExpectedNetProfitTry,
    decimal ExpectedMarginPct,
    int EstimatedOrders,
    decimal GrowthRatePct,
    decimal EstimatedBankPayoutTry,
    decimal EstimatedReserveReleaseTry,
    int ProjectedViews = 0,
    int ProjectedVisits = 0,
    int ProjectedFavorites = 0,
    int ProjectedAddToCarts = 0);

public sealed record AiForecastScenarioItemDto(
    string ScenarioCode,
    string ScenarioTitle,
    string BadgeText,
    decimal ExpectedGrossUsd,
    decimal ExpectedGrossTry,
    decimal ExpectedNetProfitUsd,
    decimal ExpectedNetProfitTry,
    decimal ExpectedMarginPct,
    int EstimatedOrders,
    string KeyDriverSummary);

public sealed record AiProductForecastItemDto(
    string ProductTitle,
    int CurrentOrders,
    decimal CurrentRevenueUsd,
    decimal CurrentProfitUsd,
    decimal CurrentMarginPct,
    int ProjectedNext30dOrders,
    decimal ProjectedNext30dRevenueUsd,
    decimal ProjectedNext30dProfitUsd,
    string AiStrategyTag,
    string AiRecommendation,
    int Views = 0,
    int Visits = 0,
    int Favorites = 0,
    int EstimatedCarts = 0,
    decimal ConversionRatePct = 0m,
    int IntentScore = 0,
    string FunnelLeakStage = "",
    decimal MaxSafeCouponPct = 20.0m,
    decimal MaxSafeCouponWithOffsiteAdsPct = 12.0m,
    int RecommendedFavCouponPct = 10,
    int RecommendedCartCouponPct = 15,
    string OfferLadderSummary = "",
    string UpsellBundleSuggestion = "");

public sealed record AiCouponGuardrailDto(
    decimal ItemPriceUsd,
    decimal ShippingChargedUsd,
    decimal UnitTotalCostUsd,
    decimal TargetMinProfitUsd,
    decimal ActiveCouponPct,
    decimal MaxSafeCouponStandardPct,
    decimal MaxSafeCouponWithOffsiteAdsPct,
    decimal BreakEvenCouponWithOffsiteAdsPct,
    decimal CustomerPaidAfterCouponUsd,
    decimal EtsyFeesUsd,
    decimal OffsiteAdFeeIfTriggeredUsd,
    decimal NetPayoutBeforeCostUsd,
    decimal NetProfitStandardUsd,
    decimal NetProfitStandardTry,
    decimal NetProfitWithOffsiteAdsUsd,
    decimal NetProfitWithOffsiteAdsTry,
    int RecommendedFavCouponPct,
    int RecommendedCartCouponPct,
    int RecommendedVipCouponPct,
    string RiskCode,
    string RiskBadge,
    string GuardrailDiagnosis);

public sealed record AiPriceElasticityScenarioDto(
    string ScenarioCode,
    string ScenarioTitle,
    string BadgeText,
    decimal ItemListPriceUsd,
    decimal ShippingChargedUsd,
    decimal CouponDiscountPct,
    decimal CustomerPaysTotalUsd,
    decimal EtsyFeesUsd,
    decimal UnitCostUsd,
    decimal UnitNetProfitUsd,
    decimal UnitNetProfitTry,
    decimal EstimatedConversionRatePct,
    decimal BreakEvenConversionRatePct,
    int EstimatedMonthlyOrders,
    decimal ExpectedMonthlyNetProfitUsd,
    decimal ExpectedMonthlyNetProfitTry,
    bool IsRecommended,
    string StrategyNote);

public sealed record AiOfferLadderTierDto(
    int TierOrder,
    string TierName,
    string TargetSegment,
    string TriggerTiming,
    string CouponCode,
    string DiscountSpec,
    decimal ExpectedConversionLiftPct,
    decimal UnitNetProfitAfterOfferUsd,
    string PsychologicalPrinciple);

public sealed record AiEventUrgencyHookDto(
    string EventCode,
    string EventTitle,
    string TargetAudience,
    string UrgencyHookText,
    string RecommendedAction);

public sealed record AiCroElasticitySummaryDto(
    AiCouponGuardrailDto CouponGuardrail,
    IReadOnlyList<AiPriceElasticityScenarioDto> ElasticityScenarios,
    IReadOnlyList<AiOfferLadderTierDto> OfferLadderTiers,
    IReadOnlyList<AiEventUrgencyHookDto> EventUrgencyHooks);

public sealed record AiFinancialForecastCommand(
    string ShopId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string? Provider = null,
    string? Model = null,
    string? ApiKey = null,
    decimal? ExchangeRate = null,
    decimal? CurrentGrossSalesUsd = null,
    decimal? CurrentEtsyFeesUsd = null,
    decimal? CurrentInnerAdsUsd = null,
    decimal? CurrentOffsiteAdsUsd = null,
    decimal? CurrentRefundsUsd = null,
    decimal? CurrentProductCostsUsd = null,
    decimal? CurrentRealNetProfitUsd = null,
    int? CurrentOrderCount = null,
    int? MissingCostOrderCount = null,
    string? TopProductTitle = null,
    decimal? TopProductRevenueUsd = null,
    decimal? TopProductProfitUsd = null,
    IReadOnlyList<string>? MissingCostOrderIds = null,
    int? HistoricalViews = null,
    int? HistoricalVisits = null,
    int? HistoricalFavorites = null,
    int? HistoricalAddToCarts = null,
    int? ActiveListingCount = null);

public sealed record AiFinancialForecastResultDto(
    string ShopId,
    string PeriodLabel,
    bool UsedAi,
    string Provider,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    decimal ExchangeRateUsed,
    decimal ExpectedGrossUsd,
    decimal ExpectedGrossTry,
    decimal MinGrossUsd,
    decimal MaxGrossUsd,
    decimal ExpectedNetProfitUsd,
    decimal ExpectedNetProfitTry,
    decimal ExpectedMarginPct,
    int EstimatedOrders,
    decimal MonthlyGrowthPct,
    decimal AverageOrderValueUsd,
    decimal EstimatedFilamentKg,
    int EstimatedShippingBoxes,
    int MissingCostOrderCount,
    int HealthScore,
    string HealthGrade,
    int BreakEvenOrdersPerMonth,
    decimal BreakEvenRevenueUsd,
    decimal BreakEvenRevenueTry,
    string ExecutiveSummary,
    string ProductProfitabilityAnalysis,
    string AdSpendAndRoasAnalysis,
    string RefundAndCostAlertAnalysis,
    IReadOnlyList<string> ActionPlan,
    IReadOnlyList<AiForecastHorizonItemDto>? HorizonProjections = null,
    IReadOnlyList<AiForecastScenarioItemDto>? Scenarios = null,
    IReadOnlyList<AiProductForecastItemDto>? ProductForecasts = null,
    AiTrafficFunnelSnapshotDto? TrafficFunnel = null,
    AiCroElasticitySummaryDto? CroElasticity = null);
