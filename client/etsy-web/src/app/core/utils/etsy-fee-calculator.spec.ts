import { calculateEtsyNetProfit, calculateVariationNetProfits } from './etsy-fee-calculator';
import { parseEtsyVariationsFromClipboard, parseInlineVariationOptionPrice } from './etsy-variation-parser';

describe('etsy-fee-calculator & variation calculations', () => {
  it('calculates single-item Etsy fees, shipping cost, and net profit breakdown', () => {
    const res = calculateEtsyNetProfit({
      salePriceUsd: 20,
      shippingCostUsd: 4,
      usdTryRate: 50,
      includeOffsiteAds: false
    });

    // Transaction 6.5% ($1.30) + Payment 6.5%+$0.06 ($1.36) + Reg 1.1% ($0.22) + Listing $0.20 = $3.08
    // VAT 20% on fees = $0.616 => Total Etsy fees = $3.696 (~$3.70)
    expect(res.salePriceUsd).toBe(20);
    expect(res.totalEtsyFeesUsd).toBe(3.7);
    expect(res.shippingCostUsd).toBe(4);
    expect(res.netRemainingUsd).toBe(12.3);
    expect(res.netRemainingTry).toBe(615);
    expect(res.netMarginPercent).toBe(61.5);
  });

  it('includes 15% offsite ads fee when enabled', () => {
    const withoutAds = calculateEtsyNetProfit({
      salePriceUsd: 20,
      shippingCostUsd: 0,
      usdTryRate: 50,
      includeOffsiteAds: false
    });
    const withAds = calculateEtsyNetProfit({
      salePriceUsd: 20,
      shippingCostUsd: 0,
      usdTryRate: 50,
      includeOffsiteAds: true
    });

    expect(withAds.offsiteAdsFeeUsd).toBe(3);
    expect(withAds.totalEtsyFeesUsd).toBeCloseTo(withoutAds.totalEtsyFeesUsd + 3, 2);
    expect(withAds.netRemainingUsd).toBeCloseTo(withoutAds.netRemainingUsd - 3, 2);
  });

  it('computes per-variation net profits and active min/max summary', () => {
    const summary = calculateVariationNetProfits(
      [
        { key: 'Small', price: 10, quantity: 5, active: true },
        { key: 'Medium', price: 15, quantity: 5, active: true },
        { key: 'Large', price: 25, quantity: 5, active: true },
        { key: 'XL (Sold Out)', price: 40, quantity: 0, active: false }
      ],
      10,
      2,
      50,
      false
    );

    expect(summary.rows.length).toBe(4);
    expect(summary.activeRows.length).toBe(3);
    expect(summary.minSalePriceUsd).toBe(10);
    expect(summary.maxSalePriceUsd).toBe(25);
    expect(summary.hasPriceSpread).toBeTrue();
    expect(summary.minNetRemainingUsd).toBeLessThan(summary.maxNetRemainingUsd);
    expect(summary.rows[0].breakdown.netRemainingUsd).toBe(summary.minNetRemainingUsd);
    expect(summary.rows[2].breakdown.netRemainingUsd).toBe(summary.maxNetRemainingUsd);
  });

  it('falls back to base price for variation rows whose price is 0', () => {
    const summary = calculateVariationNetProfits(
      [{ key: 'Standart', price: 0, quantity: 10, active: true }],
      18,
      3,
      50,
      false
    );

    expect(summary.rows[0].priceUsd).toBe(18);
    expect(summary.minSalePriceUsd).toBe(18);
    expect(summary.rows[0].breakdown.salePriceUsd).toBe(18);
  });

  it('parses inline USD, delta, and Turkish TL variation option prices', () => {
    expect(parseInlineVariationOptionPrice('Small ($10)', 50)).toEqual({
      cleanValue: 'Small',
      priceUsd: 10,
      deltaUsd: undefined,
      active: true
    });

    expect(parseInlineVariationOptionPrice('Large (+$5.50)', 50)).toEqual({
      cleanValue: 'Large',
      priceUsd: undefined,
      deltaUsd: 5.5,
      active: true
    });

    expect(parseInlineVariationOptionPrice('Kırmızı (250,00 TL)', 50)).toEqual({
      cleanValue: 'Kırmızı',
      priceUsd: 5,
      deltaUsd: undefined,
      active: true
    });

    expect(parseInlineVariationOptionPrice('Dev Boy (+1.250,00 TL)', 50)).toEqual({
      cleanValue: 'Dev Boy',
      priceUsd: undefined,
      deltaUsd: 25,
      active: true
    });

    expect(parseInlineVariationOptionPrice('Set (3)', 50)).toEqual({
      cleanValue: 'Set (3)',
      priceUsd: undefined,
      deltaUsd: undefined,
      active: true
    });

    expect(parseInlineVariationOptionPrice('XL ($20) (Sold out)', 50)).toEqual({
      cleanValue: 'XL',
      priceUsd: 20,
      deltaUsd: undefined,
      active: false
    });
  });

  it('parses copied Turkish Etsy variations and converts TL prices to USD', () => {
    const rawText = [
      'Şimdi 500,00 TL+',
      'Boyutunuzu seçin',
      'Bir seçenek seçin',
      'Small (500,00 TL)',
      'Medium (750,00 TL)',
      'Large (1.250,00 TL)'
    ].join('\n');

    const parsed = parseEtsyVariationsFromClipboard(rawText, 0, 50);
    expect(parsed.hasVariations).toBeTrue();
    expect(parsed.variationRows.map(r => ({ key: r.key, price: r.price }))).toEqual([
      { key: 'Small', price: 10 },
      { key: 'Medium', price: 15 },
      { key: 'Large', price: 25 }
    ]);
  });
});
