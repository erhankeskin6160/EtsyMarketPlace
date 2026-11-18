export interface EtsyNetProfitInput {
  salePriceUsd: number;
  shippingCostUsd: number;
  usdTryRate: number;
  includeOffsiteAds?: boolean;
}

export interface EtsyNetProfitBreakdown {
  salePriceUsd: number;
  transactionFeeUsd: number;
  paymentProcessingFeeUsd: number;
  regulatoryFeeUsd: number;
  listingFeeUsd: number;
  vatOnFeesUsd: number;
  offsiteAdsFeeUsd: number;
  totalEtsyFeesUsd: number;
  shippingCostUsd: number;
  netRemainingUsd: number;
  netRemainingTry: number;
  netMarginPercent: number;
}

const round2 = (v: number): number => Math.round((v + Number.EPSILON) * 100) / 100;

/**
 * Türkiye Etsy satıcı hesabı için kesintileri (İşlem %6.5 + Ödeme %6.5+₺3 +
 * Düzenleyici %1.1 + Listeleme $0.20 + %20 KDV + opsiyonel %15 Offsite Ads)
 * ve kargo maliyetini düşerek kalan net tutarı hesaplar.
 */
export function calculateEtsyNetProfit(input: EtsyNetProfitInput): EtsyNetProfitBreakdown {
  const salePriceUsd = Math.max(0, Number(input.salePriceUsd) || 0);
  const shippingCostUsd = Math.max(0, Number(input.shippingCostUsd) || 0);
  const rate = Number(input.usdTryRate) > 0 ? Number(input.usdTryRate) : 50.0;

  if (salePriceUsd <= 0) {
    const netZero = round2(-shippingCostUsd);
    return {
      salePriceUsd: 0,
      transactionFeeUsd: 0,
      paymentProcessingFeeUsd: 0,
      regulatoryFeeUsd: 0,
      listingFeeUsd: 0,
      vatOnFeesUsd: 0,
      offsiteAdsFeeUsd: 0,
      totalEtsyFeesUsd: 0,
      shippingCostUsd: round2(shippingCostUsd),
      netRemainingUsd: netZero,
      netRemainingTry: round2(netZero * rate),
      netMarginPercent: 0
    };
  }

  const transactionFeeUsd = salePriceUsd * 0.065;
  const paymentProcessingFeeUsd = (salePriceUsd * 0.065) + (3.0 / rate);
  const regulatoryFeeUsd = salePriceUsd * 0.011;
  const listingFeeUsd = 0.20;
  const vatOnFeesUsd = (transactionFeeUsd + paymentProcessingFeeUsd + regulatoryFeeUsd + listingFeeUsd) * 0.20;
  const offsiteAdsFeeUsd = input.includeOffsiteAds ? salePriceUsd * 0.15 : 0;

  const totalEtsyFeesUsd =
    transactionFeeUsd +
    paymentProcessingFeeUsd +
    regulatoryFeeUsd +
    listingFeeUsd +
    vatOnFeesUsd +
    offsiteAdsFeeUsd;

  const netRemainingUsd = round2(salePriceUsd - totalEtsyFeesUsd - shippingCostUsd);
  const netRemainingTry = round2(netRemainingUsd * rate);
  const netMarginPercent = Math.round(((netRemainingUsd / salePriceUsd) * 100) * 10) / 10;

  return {
    salePriceUsd: round2(salePriceUsd),
    transactionFeeUsd: round2(transactionFeeUsd),
    paymentProcessingFeeUsd: round2(paymentProcessingFeeUsd),
    regulatoryFeeUsd: round2(regulatoryFeeUsd),
    listingFeeUsd,
    vatOnFeesUsd: round2(vatOnFeesUsd),
    offsiteAdsFeeUsd: round2(offsiteAdsFeeUsd),
    totalEtsyFeesUsd: round2(totalEtsyFeesUsd),
    shippingCostUsd: round2(shippingCostUsd),
    netRemainingUsd,
    netRemainingTry,
    netMarginPercent
  };
}

export interface VariationProfitItemInput {
  key: string;
  price: number;
  quantity: number;
  active?: boolean;
  shippingCostUsd?: number | null;
}

export interface VariationNetProfitRow {
  key: string;
  priceUsd: number;
  quantity: number;
  active: boolean;
  breakdown: EtsyNetProfitBreakdown;
}

export interface VariationNetProfitSummary {
  rows: VariationNetProfitRow[];
  activeRows: VariationNetProfitRow[];
  minSalePriceUsd: number;
  maxSalePriceUsd: number;
  minTotalFeesUsd: number;
  maxTotalFeesUsd: number;
  minNetRemainingUsd: number;
  maxNetRemainingUsd: number;
  minNetRemainingTry: number;
  maxNetRemainingTry: number;
  minNetMarginPercent: number;
  maxNetMarginPercent: number;
  hasPriceSpread: boolean;
}

/**
 * Tüm varyasyon seçenekleri için tek tek Etsy kesinti, kargo ve net kâr kırılımını
 * hesaplar; aktif seçenekler arası taban-tavan aralığını özetler.
 */
export function calculateVariationNetProfits(
  rows: VariationProfitItemInput[],
  fallbackBasePriceUsd: number,
  defaultShippingCostUsd: number,
  usdTryRate: number,
  includeOffsiteAds = false
): VariationNetProfitSummary {
  if (!Array.isArray(rows) || rows.length === 0) {
    return {
      rows: [],
      activeRows: [],
      minSalePriceUsd: 0,
      maxSalePriceUsd: 0,
      minTotalFeesUsd: 0,
      maxTotalFeesUsd: 0,
      minNetRemainingUsd: 0,
      maxNetRemainingUsd: 0,
      minNetRemainingTry: 0,
      maxNetRemainingTry: 0,
      minNetMarginPercent: 0,
      maxNetMarginPercent: 0,
      hasPriceSpread: false
    };
  }

  const safeFallbackPrice = Math.max(0, Number(fallbackBasePriceUsd) || 0);
  const safeDefaultShipping = Math.max(0, Number(defaultShippingCostUsd) || 0);

  const computedRows: VariationNetProfitRow[] = rows.map(row => {
    const rawPrice = Number(row.price);
    const effectivePrice = Number.isFinite(rawPrice) && rawPrice > 0 ? round2(rawPrice) : round2(safeFallbackPrice);
    const effectiveShipping =
      row.shippingCostUsd !== undefined && row.shippingCostUsd !== null
        ? Math.max(0, Number(row.shippingCostUsd) || 0)
        : safeDefaultShipping;

    const breakdown = calculateEtsyNetProfit({
      salePriceUsd: effectivePrice,
      shippingCostUsd: effectiveShipping,
      usdTryRate,
      includeOffsiteAds
    });

    return {
      key: row.key,
      priceUsd: effectivePrice,
      quantity: Math.max(0, Number(row.quantity) || 0),
      active: row.active !== false,
      breakdown
    };
  });

  const activeRows = computedRows.filter(r => r.active);
  const targetRows = activeRows.length > 0 ? activeRows : computedRows;

  const minSalePriceUsd = round2(Math.min(...targetRows.map(r => r.priceUsd)));
  const maxSalePriceUsd = round2(Math.max(...targetRows.map(r => r.priceUsd)));
  const minTotalFeesUsd = round2(Math.min(...targetRows.map(r => r.breakdown.totalEtsyFeesUsd)));
  const maxTotalFeesUsd = round2(Math.max(...targetRows.map(r => r.breakdown.totalEtsyFeesUsd)));
  const minNetRemainingUsd = round2(Math.min(...targetRows.map(r => r.breakdown.netRemainingUsd)));
  const maxNetRemainingUsd = round2(Math.max(...targetRows.map(r => r.breakdown.netRemainingUsd)));
  const minNetRemainingTry = round2(Math.min(...targetRows.map(r => r.breakdown.netRemainingTry)));
  const maxNetRemainingTry = round2(Math.max(...targetRows.map(r => r.breakdown.netRemainingTry)));
  const minNetMarginPercent = Math.min(...targetRows.map(r => r.breakdown.netMarginPercent));
  const maxNetMarginPercent = Math.max(...targetRows.map(r => r.breakdown.netMarginPercent));

  return {
    rows: computedRows,
    activeRows,
    minSalePriceUsd,
    maxSalePriceUsd,
    minTotalFeesUsd,
    maxTotalFeesUsd,
    minNetRemainingUsd,
    maxNetRemainingUsd,
    minNetRemainingTry,
    maxNetRemainingTry,
    minNetMarginPercent,
    maxNetMarginPercent,
    hasPriceSpread: Math.abs(maxSalePriceUsd - minSalePriceUsd) >= 0.01
  };
}
