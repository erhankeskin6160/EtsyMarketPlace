export interface PaymentLedgerEntry {
  id: string;
  entryDate: string;
  transactionType: 'sale' | 'fee' | 'refund' | 'ad' | 'listing' | 'payout';
  typeDisplay: string;
  title: string;
  orderNumber?: string;
  grossAmount: number;
  feeAmount: number;
  netAmount: number;
  currency: string;
  netAmountTry: number;
  runningBalance: number;
}

export interface BankPayoutRecord {
  payoutId: string;
  initiatedDate: string;
  completedDate?: string;
  status: 'completed' | 'processing' | 'pending';
  bankName: string;
  ibanEnding: string;
  amountUsd: number;
  exchangeRate: number;
  amountTry: number;
  referenceNumber: string;
}

export interface FinancialKpiSummary {
  period: string;
  currency: string;
  grossSales: number;
  etsyFees: number;
  innerAds: number;
  offsiteAds: number;
  refunds: number;
  netRevenue: number;
  productCosts: number;
  shippingCosts: number;
  realNetProfit: number;
  profitMarginPercent: number;
  bankPayoutsTotal: number;
}

export interface ExpenseBreakdownItem {
  name: string;
  amountUsd: number;
  amountTry: number;
  percentage: number;
  color: string;
}

export interface OrderFinancialRow {
  orderDate: string;
  receiptId: string;
  orderStatus: 'completed' | 'canceled' | 'refunded' | 'open';
  displayStatus: string;
  buyerName: string;
  productTitle: string;
  quantity: number;
  grandTotalUsd: number;
  etsyFeesUsd: number;
  offsiteAdFeeUsd: number;
  productCostUsd: number | null;
  netProfitUsd: number;
  exchangeRate: number;
  netProfitTry: number;
  hasCostData: boolean;
  hasInvoice: boolean;
  invoiceName?: string;
  unitProductCostUsd?: number | null;
  actualShippingCostUsd?: number | null;
  packagingCostUsd?: number | null;
  costNotes?: string | null;
  effectiveExchangeRate?: number;
  etsyNetPayoutUsd?: number;
  etsyNetPayoutTry?: number;
  fxConversionFeeTry?: number;
  refundDate?: string;
  originalOrderDate?: string;
  refundAmountUsd?: number;
  refundAmountTry?: number;
}

export interface ForecastKpiSummary {
  expectedGrossUsd: number;
  expectedGrossTry: number;
  minGrossUsd: number;
  maxGrossUsd: number;
  expectedProfitUsd: number;
  expectedProfitTry: number;
  profitMarginPct: number;
  estimatedOrders: number;
  monthlyGrowthPct: number;
  filamentKg: number;
  shippingBoxesCount: number;
}

export interface AiTrafficFunnelSnapshotDto {
  activeListingCount: number;
  totalViews: number;
  estimatedUniqueVisits: number;
  totalFavorites: number;
  estimatedAddToCarts: number;
  realizedOrders: number;
  viewToVisitRatePct: number;
  favoriteToViewRatePct: number;
  addToCartRatePct: number;
  cartToOrderConversionPct: number;
  overallConversionRatePct: number;
  projectedNext30dViews: number;
  projectedNext30dVisits: number;
  projectedNext30dFavorites: number;
  projectedNext30dCarts: number;
  funnelBottleneckStage: string;
  funnelAiDiagnosis: string;
}

export interface AiFinancialForecastRequestDto {
  shopId?: string;
  startDate: string;
  endDate: string;
  provider?: string;
  model?: string;
  apiKey?: string;
  exchangeRate?: number;
  currentGrossSalesUsd?: number;
  currentEtsyFeesUsd?: number;
  currentInnerAdsUsd?: number;
  currentOffsiteAdsUsd?: number;
  currentRefundsUsd?: number;
  currentProductCostsUsd?: number;
  currentRealNetProfitUsd?: number;
  currentOrderCount?: number;
  missingCostOrderCount?: number;
  topProductTitle?: string | null;
  topProductRevenueUsd?: number;
  topProductProfitUsd?: number;
  missingCostOrderIds?: string[];
  historicalViews?: number;
  historicalVisits?: number;
  historicalFavorites?: number;
  historicalAddToCarts?: number;
  activeListingCount?: number;
}

export interface AiForecastHorizonItemDto {
  horizonCode: string;
  horizonTitle: string;
  days: number;
  expectedGrossUsd: number;
  expectedGrossTry: number;
  expectedFeesAndAdsUsd: number;
  expectedCostsUsd: number;
  expectedNetProfitUsd: number;
  expectedNetProfitTry: number;
  expectedMarginPct: number;
  estimatedOrders: number;
  growthRatePct: number;
  estimatedBankPayoutTry: number;
  estimatedReserveReleaseTry: number;
  projectedViews?: number;
  projectedVisits?: number;
  projectedFavorites?: number;
  projectedAddToCarts?: number;
}

export interface AiForecastScenarioItemDto {
  scenarioCode: string;
  scenarioTitle: string;
  badgeText: string;
  expectedGrossUsd: number;
  expectedGrossTry: number;
  expectedNetProfitUsd: number;
  expectedNetProfitTry: number;
  expectedMarginPct: number;
  estimatedOrders: number;
  keyDriverSummary: string;
}

export interface AiProductForecastItemDto {
  productTitle: string;
  currentOrders: number;
  currentRevenueUsd: number;
  currentProfitUsd: number;
  currentMarginPct: number;
  projectedNext30dOrders: number;
  projectedNext30dRevenueUsd: number;
  projectedNext30dProfitUsd: number;
  views?: number;
  visits?: number;
  favorites?: number;
  estimatedCarts?: number;
  conversionRatePct?: number;
  intentScore?: number;
  funnelLeakStage?: string;
  maxSafeCouponPct?: number;
  maxSafeCouponWithOffsiteAdsPct?: number;
  recommendedFavCouponPct?: number;
  recommendedCartCouponPct?: number;
  offerLadderSummary?: string;
  upsellBundleSuggestion?: string;
  aiStrategyTag: string;
  aiRecommendation: string;
}

export interface AiCouponGuardrailDto {
  itemPriceUsd: number;
  shippingChargedUsd: number;
  unitTotalCostUsd: number;
  targetMinProfitUsd: number;
  activeCouponPct: number;
  maxSafeCouponStandardPct: number;
  maxSafeCouponWithOffsiteAdsPct: number;
  breakEvenCouponWithOffsiteAdsPct: number;
  customerPaidAfterCouponUsd: number;
  etsyFeesUsd: number;
  offsiteAdFeeIfTriggeredUsd: number;
  netPayoutBeforeCostUsd: number;
  netProfitStandardUsd: number;
  netProfitStandardTry: number;
  netProfitWithOffsiteAdsUsd: number;
  netProfitWithOffsiteAdsTry: number;
  recommendedFavCouponPct: number;
  recommendedCartCouponPct: number;
  recommendedVipCouponPct: number;
  riskCode: string;
  riskBadge: string;
  guardrailDiagnosis: string;
}

export interface AiPriceElasticityScenarioDto {
  scenarioCode: string;
  scenarioTitle: string;
  badgeText: string;
  itemListPriceUsd: number;
  shippingChargedUsd: number;
  couponDiscountPct: number;
  customerPaysTotalUsd: number;
  etsyFeesUsd: number;
  unitCostUsd: number;
  unitNetProfitUsd: number;
  unitNetProfitTry: number;
  estimatedConversionRatePct: number;
  breakEvenConversionRatePct: number;
  estimatedMonthlyOrders: number;
  expectedMonthlyNetProfitUsd: number;
  expectedMonthlyNetProfitTry: number;
  isRecommended: boolean;
  strategyNote: string;
}

export interface AiOfferLadderTierDto {
  tierOrder: number;
  tierName: string;
  targetSegment: string;
  triggerTiming: string;
  couponCode: string;
  discountSpec: string;
  expectedConversionLiftPct: number;
  unitNetProfitAfterOfferUsd: number;
  psychologicalPrinciple: string;
}

export interface AiEventUrgencyHookDto {
  eventCode: string;
  eventTitle: string;
  targetAudience: string;
  urgencyHookText: string;
  recommendedAction: string;
}

export interface AiCroElasticitySummaryDto {
  couponGuardrail: AiCouponGuardrailDto;
  elasticityScenarios: AiPriceElasticityScenarioDto[];
  offerLadderTiers: AiOfferLadderTierDto[];
  eventUrgencyHooks: AiEventUrgencyHookDto[];
}

export interface AiFinancialForecastResponseDto {
  shopId: string;
  periodLabel: string;
  usedAi: boolean;
  provider: string;
  model: string;
  generatedAtUtc: string;
  exchangeRateUsed: number;
  expectedGrossUsd: number;
  expectedGrossTry: number;
  minGrossUsd: number;
  maxGrossUsd: number;
  expectedNetProfitUsd: number;
  expectedNetProfitTry: number;
  expectedMarginPct: number;
  estimatedOrders: number;
  monthlyGrowthPct: number;
  averageOrderValueUsd: number;
  estimatedFilamentKg: number;
  estimatedShippingBoxes: number;
  missingCostOrderCount: number;
  healthScore?: number;
  healthGrade?: string;
  breakEvenOrdersPerMonth?: number;
  breakEvenRevenueUsd?: number;
  breakEvenRevenueTry?: number;
  executiveSummary: string;
  productProfitabilityAnalysis: string;
  adSpendAndRoasAnalysis: string;
  refundAndCostAlertAnalysis: string;
  actionPlan: string[];
  horizonProjections?: AiForecastHorizonItemDto[];
  scenarios?: AiForecastScenarioItemDto[];
  productForecasts?: AiProductForecastItemDto[];
  trafficFunnel?: AiTrafficFunnelSnapshotDto;
  croElasticity?: AiCroElasticitySummaryDto;
}
