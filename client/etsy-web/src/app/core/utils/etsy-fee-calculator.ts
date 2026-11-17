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
