using System;
using System.Collections.Generic;
using System.Globalization;

namespace EtsyMarketPlace.Application.EtsyIntegration;

/// <summary>
/// Çoklu Yapay Zeka Konseyi (Qwen-3.8, Gemma-4-31B, DeepSeek-V4.1 ve Antigravity)
/// matematiksel modellerini uygulayan Dönüşüm Hunisi (CRO), Kupon Çakışması Kalkanı (C_max)
/// ve Fiyat / Ücretsiz Kargo Elastikiyet Simülasyon Motoru.
/// </summary>
public static class EtsyCroElasticityEngine
{
    private const decimal DefaultEtsyFeeRatio = 0.2205m; // %6.5 işlem + %6.5 ödeme + %1.67 yasal + KDV (~%22.05)
    private const decimal DefaultOffsiteAdsRatio = 0.15m; // %15 Offsite Ads komisyonu

    /// <summary>
    /// Bir ürün için Kupon + Offsite Ads + Etsy Kesintisi + 3D Baskı/Kargo Maliyeti üst üste bindiğinde
    /// zarar etmeyi önleyen Maksimum Güvenli Kupon Oranını (C_max) ve aktif kuponun risk durumunu hesaplar.
    /// </summary>
    public static AiCouponGuardrailDto CalculateCouponGuardrail(
        decimal itemPriceUsd,
        decimal shippingChargedUsd,
        decimal unitTotalCostUsd,
        decimal targetMinProfitUsd = 5.00m,
        decimal activeCouponPct = 30.0m,
        decimal etsyFeeRatio = DefaultEtsyFeeRatio,
        decimal offsiteAdsRatio = DefaultOffsiteAdsRatio,
        decimal exchangeRate = 49.50m)
    {
        var safePrice = Math.Max(1.0m, itemPriceUsd);
        var safeShip = Math.Max(0.0m, shippingChargedUsd);
        var safeCost = Math.Max(0.0m, unitTotalCostUsd);
        var safeTarget = Math.Max(0.0m, targetMinProfitUsd);
        var clampedCoupon = Math.Clamp(activeCouponPct, 0.0m, 80.0m) / 100m;
        var rate = exchangeRate > 10m ? exchangeRate : 49.50m;

        var keepRatioStandard = Math.Max(0.10m, 1.0m - etsyFeeRatio);
        var keepRatioWithOffsite = Math.Max(0.10m, 1.0m - etsyFeeRatio - offsiteAdsRatio);

        // C_max = max(0, [P + S - (Target + Cost) / (1 - F)] / P) — canlı net kesinti oranı (~%22.05 sabit pay dahil) ile birebir tutarlı
        var requiredGrossStandard = (safeTarget + safeCost) / keepRatioStandard;
        var maxSafeStandardRatio = (safePrice + safeShip - requiredGrossStandard) / safePrice;
        var maxSafeStandardPct = Math.Round(Math.Clamp(maxSafeStandardRatio * 100m, 0m, 65m), 1);

        var requiredGrossOffsite = (safeTarget + safeCost) / keepRatioWithOffsite;
        var maxSafeOffsiteRatio = (safePrice + safeShip - requiredGrossOffsite) / safePrice;
        var maxSafeOffsitePct = Math.Round(Math.Clamp(maxSafeOffsiteRatio * 100m, 0m, 55m), 1);

        // Zarar başabaş (Target = 0) kupon sınırı (Offsite Ads dahil)
        var breakEvenGrossOffsite = safeCost / keepRatioWithOffsite;
        var breakEvenCouponOffsitePct = Math.Round(Math.Clamp(((safePrice + safeShip - breakEvenGrossOffsite) / safePrice) * 100m, 0m, 75m), 1);

        // Aktif kupon (örn. %30 DSFJY25) ile gerçekleşen rakamlar
        var discountedSubtotal = Math.Round(safePrice * (1.0m - clampedCoupon), 2);
        var customerTotalPaidBeforeTax = Math.Round(discountedSubtotal + safeShip, 2);
        var etsyFeesUsd = Math.Round((customerTotalPaidBeforeTax * etsyFeeRatio), 2);
        var netPayoutStandardUsd = Math.Round(customerTotalPaidBeforeTax - etsyFeesUsd, 2);
        var netProfitStandardUsd = Math.Round(netPayoutStandardUsd - safeCost, 2);

        var offsiteFeeUsd = Math.Round(customerTotalPaidBeforeTax * offsiteAdsRatio, 2);
        var netPayoutOffsiteUsd = Math.Round(netPayoutStandardUsd - offsiteFeeUsd, 2);
        var netProfitOffsiteUsd = Math.Round(netPayoutOffsiteUsd - safeCost, 2);

        string riskCode;
        string riskBadge;
        string diagnosis;

        if (netProfitOffsiteUsd < 0m)
        {
            riskCode = "CRITICAL_LOSS_TRAP";
            riskBadge = "🛑 ZARAR TUZAĞI (Kupon + Offsite Ads Çakışması)";
            diagnosis = $"%{activeCouponPct:0} kupon uygulandığında ürün + kargo tahsilatı ${customerTotalPaidBeforeTax:0.00} seviyesine düşüyor. Standart satışta ${netProfitStandardUsd:0.00} kâr kalırken, sipariş Offsite Ads (%15 = -${offsiteFeeUsd:0.00}) üzerinden gelirse net sonuç -${Math.Abs(netProfitOffsiteUsd):0.00} (-₺{Math.Abs(Math.Round(netProfitOffsiteUsd * rate, 2)):N2}) ZARARA dönüyor! Güvenli maksimum kupon sınırınız %{maxSafeOffsitePct:0.0} (Offsite Ads korumalı) veya %{maxSafeStandardPct:0.0} (standart) olmalıdır.";
        }
        else if (netProfitOffsiteUsd < safeTarget)
        {
            riskCode = "WARNING_MARGIN_EROSION";
            riskBadge = "⚠️ MARJ ERİMESİ UYARISI (Hedef Kâr Altı)";
            diagnosis = $"%{activeCouponPct:0} kupon ile standart satışta ${netProfitStandardUsd:0.00} net kâr kalıyor; ancak Offsite Ads (%15) çakışmasında net kâr ${netProfitOffsiteUsd:0.00} seviyesine gerileyerek hedef kârınızın (${safeTarget:0.00}) altına iniyor. İdeal sepet/favori kupon koridoru: %{Math.Min(15m, maxSafeStandardPct):0} – %{maxSafeStandardPct:0.0}.";
        }
        else
        {
            riskCode = "SAFE_GUARDRAIL";
            riskBadge = "🟢 GÜVENLİ MARJ KORİDORU";
            diagnosis = $"%{activeCouponPct:0} kupon oranınız hem standart satışta (${netProfitStandardUsd:0.00} net kâr) hem de Offsite Ads (%15) çakışmasında (${netProfitOffsiteUsd:0.00} net kâr) hedef marjınızı koruyor.";
        }

        var recFavPct = (int)Math.Clamp(Math.Round(Math.Min(10m, maxSafeStandardPct * 0.55m)), 5, 15);
        var recCartPct = (int)Math.Clamp(Math.Round(Math.Min(15m, maxSafeStandardPct * 0.75m)), 5, 20);
        var recVipPct = (int)Math.Clamp(Math.Round(Math.Min(20m, maxSafeStandardPct)), 10, 25);

        return new AiCouponGuardrailDto(
            ItemPriceUsd: safePrice,
            ShippingChargedUsd: safeShip,
            UnitTotalCostUsd: safeCost,
            TargetMinProfitUsd: safeTarget,
            ActiveCouponPct: Math.Round(activeCouponPct, 1),
            MaxSafeCouponStandardPct: maxSafeStandardPct,
            MaxSafeCouponWithOffsiteAdsPct: maxSafeOffsitePct,
            BreakEvenCouponWithOffsiteAdsPct: breakEvenCouponOffsitePct,
            CustomerPaidAfterCouponUsd: customerTotalPaidBeforeTax,
            EtsyFeesUsd: etsyFeesUsd,
            OffsiteAdFeeIfTriggeredUsd: offsiteFeeUsd,
            NetPayoutBeforeCostUsd: netPayoutStandardUsd,
            NetProfitStandardUsd: netProfitStandardUsd,
            NetProfitStandardTry: Math.Round(netProfitStandardUsd * rate, 2),
            NetProfitWithOffsiteAdsUsd: netProfitOffsiteUsd,
            NetProfitWithOffsiteAdsTry: Math.Round(netProfitOffsiteUsd * rate, 2),
            RecommendedFavCouponPct: recFavPct,
            RecommendedCartCouponPct: recCartPct,
            RecommendedVipCouponPct: recVipPct,
            RiskCode: riskCode,
            RiskBadge: riskBadge,
            GuardrailDiagnosis: diagnosis);
    }

    /// <summary>
    /// 4 farklı Fiyat, Kargo ve Kupon senaryosunun (Ücretli Kargo vs %15 Sepet Kuponu vs Ücretsiz Kargo vs %30 Agresif Kupon)
    /// dönüşüm oranını (CVR), başabaş dönüşüm eşiğini (Break-Even CVR) ve 30 günlük beklenen net kârını simüle eder.
    /// </summary>
    public static List<AiPriceElasticityScenarioDto> BuildPriceShippingElasticityMatrix(
        decimal baseItemPriceUsd,
        decimal baseShippingChargedUsd,
        decimal unitTotalCostUsd,
        int projectedMonthlyVisits,
        decimal baseConversionRatePct,
        decimal exchangeRate = 49.50m)
    {
        var p = baseItemPriceUsd > 0m ? baseItemPriceUsd : 45.00m;
        var s = baseShippingChargedUsd >= 0m ? baseShippingChargedUsd : 10.00m;
        var cost = unitTotalCostUsd > 0m ? unitTotalCostUsd : 12.50m;
        var visits = Math.Max(120, projectedMonthlyVisits);
        var cr = Math.Clamp(baseConversionRatePct > 0m ? baseConversionRatePct : 2.10m, 0.45m, 12.0m);
        var rate = exchangeRate > 10m ? exchangeRate : 49.50m;

        // Senaryo 1: Mevcut Liste ($45 ürün + $10 kargo, %0 kupon)
        var s1Total = p + s;
        var s1Fees = Math.Round(s1Total * DefaultEtsyFeeRatio, 2);
        var s1UnitProfit = Math.Round(s1Total - s1Fees - cost, 2);
        var s1Orders = Math.Max(1, (int)Math.Round(visits * (cr / 100m)));
        var s1MonthlyProfitUsd = Math.Round(s1Orders * s1UnitProfit, 2);

        static decimal ComputeBreakEvenCr(decimal baseCrVal, decimal baseProfitVal, decimal scenarioProfitVal, decimal fallbackOnUnprofitable)
        {
            if (scenarioProfitVal <= 0m) return fallbackOnUnprofitable;
            if (baseProfitVal <= 0m) return 0m;
            return Math.Round(baseCrVal * (baseProfitVal / scenarioProfitVal), 2);
        }

        // Senaryo 2: Akıllı Sepet/Favori Kupon Kurgusu (+%8 Liste Fiyatı Çapası + %15 Kupon)
        var s2ListPrice = Math.Round(p * 1.08m, 2);
        var s2CouponPct = 15m;
        var s2DiscountedItem = Math.Round(s2ListPrice * (1m - s2CouponPct / 100m), 2);
        var s2Total = s2DiscountedItem + s;
        var s2Fees = Math.Round(s2Total * DefaultEtsyFeeRatio, 2);
        var s2UnitProfit = Math.Round(s2Total - s2Fees - cost, 2);
        var s2Cr = Math.Round(cr * 1.24m, 2); // %15 sepet kuponu psikolojik tetiklemesi (+%24 CVR)
        var s2Orders = Math.Max(1, (int)Math.Round(visits * (s2Cr / 100m)));
        var s2MonthlyProfitUsd = Math.Round(s2Orders * s2UnitProfit, 2);
        var s2BreakEvenCr = ComputeBreakEvenCr(cr, s1UnitProfit, s2UnitProfit, 99.9m);

        // Senaryo 3: Ücretsiz Kargo Psikolojisi ($55 Ürün + $0 Kargo — Etsy US $35+ Free Shipping Boost)
        var s3ListPrice = Math.Round(p + s, 2);
        var s3Total = s3ListPrice;
        var s3Fees = Math.Round(s3Total * DefaultEtsyFeeRatio, 2);
        var s3UnitProfit = Math.Round(s3Total - s3Fees - cost, 2);
        var s3Cr = Math.Round(cr * 1.28m, 2); // Kargo şokunun kalkması + Etsy Free Shipping rozeti (+%28 CVR)
        var s3Orders = Math.Max(1, (int)Math.Round(visits * (s3Cr / 100m)));
        var s3MonthlyProfitUsd = Math.Round(s3Orders * s3UnitProfit, 2);
        var s3BreakEvenCr = ComputeBreakEvenCr(cr, s1UnitProfit, s3UnitProfit, cr);

        // Senaryo 4: Agresif %30 Mağaza Kuponu (Örn: DSFJY25 — $45 ürün -> $31.50 + $10 kargo = $41.50)
        var s4CouponPct = 30m;
        var s4DiscountedItem = Math.Round(p * (1m - s4CouponPct / 100m), 2);
        var s4Total = s4DiscountedItem + s;
        var s4Fees = Math.Round(s4Total * DefaultEtsyFeeRatio, 2);
        var s4UnitProfit = Math.Round(s4Total - s4Fees - cost, 2);
        var s4Cr = Math.Round(cr * 1.38m, 2);
        var s4Orders = Math.Max(1, (int)Math.Round(visits * (s4Cr / 100m)));
        var s4MonthlyProfitUsd = Math.Round(s4Orders * s4UnitProfit, 2);
        var s4BreakEvenCr = ComputeBreakEvenCr(cr, s1UnitProfit, s4UnitProfit, 99.9m);

        return
        [
            new AiPriceElasticityScenarioDto(
                ScenarioCode: "PAID_SHIPPING_BASE",
                ScenarioTitle: "📦 Mevcut Liste (Ücretli Kargo, %0 Kupon)",
                BadgeText: "Baz Senaryo",
                ItemListPriceUsd: p,
                ShippingChargedUsd: s,
                CouponDiscountPct: 0m,
                CustomerPaysTotalUsd: s1Total,
                EtsyFeesUsd: s1Fees,
                UnitCostUsd: cost,
                UnitNetProfitUsd: s1UnitProfit,
                UnitNetProfitTry: Math.Round(s1UnitProfit * rate, 2),
                EstimatedConversionRatePct: cr,
                BreakEvenConversionRatePct: cr,
                EstimatedMonthlyOrders: s1Orders,
                ExpectedMonthlyNetProfitUsd: s1MonthlyProfitUsd,
                ExpectedMonthlyNetProfitTry: Math.Round(s1MonthlyProfitUsd * rate, 2),
                IsRecommended: false,
                StrategyNote: $"Müşteri sepette +${s:0.00} kargo eklenince %60+ oranda ödeme adımında vazgeçebiliyor (Shipping Shock)."),

            new AiPriceElasticityScenarioDto(
                ScenarioCode: "SMART_ANCHOR_COUPON_15",
                ScenarioTitle: "🎯 Akıllı Çapa Fiyat + %15 Sepet/Fav Kuponu",
                BadgeText: "En Yüksek Marj Koruması",
                ItemListPriceUsd: s2ListPrice,
                ShippingChargedUsd: s,
                CouponDiscountPct: s2CouponPct,
                CustomerPaysTotalUsd: s2Total,
                EtsyFeesUsd: s2Fees,
                UnitCostUsd: cost,
                UnitNetProfitUsd: s2UnitProfit,
                UnitNetProfitTry: Math.Round(s2UnitProfit * rate, 2),
                EstimatedConversionRatePct: s2Cr,
                BreakEvenConversionRatePct: s2BreakEvenCr,
                EstimatedMonthlyOrders: s2Orders,
                ExpectedMonthlyNetProfitUsd: s2MonthlyProfitUsd,
                ExpectedMonthlyNetProfitTry: Math.Round(s2MonthlyProfitUsd * rate, 2),
                IsRecommended: s2MonthlyProfitUsd >= s3MonthlyProfitUsd,
                StrategyNote: $"Liste fiyatını ${s2ListPrice:0.00} yapıp terk edilmiş sepet/favori kitlesine %15 kupon (CART15) verdiğinizde hem indirim psikolojisi çalışır hem birim kâr ${s2UnitProfit:0.00} korunur."),

            new AiPriceElasticityScenarioDto(
                ScenarioCode: "FREE_SHIPPING_PSYCHOLOGY",
                ScenarioTitle: "🚀 Ücretsiz Kargo Psikolojisi ($35+ ABD Algoritma Boost)",
                BadgeText: "🌟 AI Önerisi (En Yüksek Dönüşüm)",
                ItemListPriceUsd: s3ListPrice,
                ShippingChargedUsd: 0m,
                CouponDiscountPct: 0m,
                CustomerPaysTotalUsd: s3Total,
                EtsyFeesUsd: s3Fees,
                UnitCostUsd: cost,
                UnitNetProfitUsd: s3UnitProfit,
                UnitNetProfitTry: Math.Round(s3UnitProfit * rate, 2),
                EstimatedConversionRatePct: s3Cr,
                BreakEvenConversionRatePct: s3BreakEvenCr,
                EstimatedMonthlyOrders: s3Orders,
                ExpectedMonthlyNetProfitUsd: s3MonthlyProfitUsd,
                ExpectedMonthlyNetProfitTry: Math.Round(s3MonthlyProfitUsd * rate, 2),
                IsRecommended: s3MonthlyProfitUsd >= s2MonthlyProfitUsd,
                StrategyNote: $"${s:0.00} kargo ücretini ürün fiyatına (${s3ListPrice:0.00}) dahil ettiğinizde toplam müşteri ödemesi değişmez, Etsy kesintisi kuruşu kuruşuna aynı kalır; ancak sepette 'Kargo Şoku' kalktığı için dönüşüm %28 artar."),

            new AiPriceElasticityScenarioDto(
                ScenarioCode: "AGGRESSIVE_COUPON_30",
                ScenarioTitle: "⚠️ %30 Mağaza Kuponu (Örn: DSFJY25 Kampanyası)",
                BadgeText: s4UnitProfit < 10m ? "Marj Erimesi Riski" : "Yüksek İndirim",
                ItemListPriceUsd: p,
                ShippingChargedUsd: s,
                CouponDiscountPct: s4CouponPct,
                CustomerPaysTotalUsd: s4Total,
                EtsyFeesUsd: s4Fees,
                UnitCostUsd: cost,
                UnitNetProfitUsd: s4UnitProfit,
                UnitNetProfitTry: Math.Round(s4UnitProfit * rate, 2),
                EstimatedConversionRatePct: s4Cr,
                BreakEvenConversionRatePct: s4BreakEvenCr,
                EstimatedMonthlyOrders: s4Orders,
                ExpectedMonthlyNetProfitUsd: s4MonthlyProfitUsd,
                ExpectedMonthlyNetProfitTry: Math.Round(s4MonthlyProfitUsd * rate, 2),
                IsRecommended: false,
                StrategyNote: $"Order #4225379961 senaryosu: Müşteri ${s4Total:0.00} öder, Etsy -${s4Fees:0.00} keser ve maliyet öncesi ${Math.Round(s4Total - s4Fees, 2):0.00} kalır. Bu senaryonun baz senaryoyu geçmesi için dönüşümün en az %{s4BreakEvenCr:0.00} olması gerekir.")
        ];
    }

    /// <summary>
    /// Ürün bazlı Görüntülenme, Ziyaret, Favori, Sepet ve Sipariş verilerinden
    /// Niyet Skoru (0-100), Huni Kaçak Noktası, Kademeli Kupon ve Aynı Kargoda Bundle önerisi üretir.
    /// </summary>
    public static (
        int IntentScore,
        string FunnelLeakStage,
        decimal MaxSafeCouponPct,
        decimal MaxSafeCouponWithOffsiteAdsPct,
        int RecommendedFavCouponPct,
        int RecommendedCartCouponPct,
        string OfferLadderSummary,
        string UpsellBundleSuggestion) AnalyzeProductCroMetrics(
        string productTitle,
        int views,
        int visits,
        int favorites,
        int estimatedCarts,
        int orders,
        decimal avgUnitPriceUsd,
        decimal avgUnitProfitUsd)
    {
        var safeViews = Math.Max(1, views);
        var safeVisits = Math.Max(1, visits);
        var favRatePct = (decimal)favorites / safeViews * 100m;
        var cartRatePct = (decimal)estimatedCarts / safeVisits * 100m;
        var checkoutRatePct = estimatedCarts > 0 ? (decimal)orders / estimatedCarts * 100m : 0m;

        // Intent Score (0 - 100): Favori yoğunluğu + Sepet yoğunluğu + Sipariş sinyali
        var rawScore = 35
            + (int)Math.Min(30m, favRatePct * 3.8m)
            + (int)Math.Min(25m, cartRatePct * 2.5m)
            + (orders > 0 ? Math.Min(10, orders * 3) : 0);
        var intentScore = Math.Clamp(rawScore, 18, 98);

        var estPrice = avgUnitPriceUsd > 5m ? avgUnitPriceUsd : 45.00m;
        var estCost = avgUnitProfitUsd > 0m && avgUnitPriceUsd > avgUnitProfitUsd
            ? Math.Max(6.0m, (avgUnitPriceUsd * 0.78m) - avgUnitProfitUsd)
            : Math.Round(estPrice * 0.28m, 2);

        var guardrail = CalculateCouponGuardrail(
            itemPriceUsd: estPrice,
            shippingChargedUsd: 10.0m,
            unitTotalCostUsd: estCost,
            targetMinProfitUsd: 5.0m,
            activeCouponPct: 15.0m);

        string leakStage;
        if (favRatePct < 3.5m)
        {
            leakStage = "👁️ Vitrin & CTR Kaçağı (Görsel / İlk 40 Karakter)";
        }
        else if (favorites >= 6 && estimatedCarts <= 1)
        {
            leakStage = "❤️ Favoride Bekleme (Fiyat Çapası / Yem Varyant Eksik)";
        }
        else if (estimatedCarts > orders * 2 && checkoutRatePct < 38m)
        {
            leakStage = "🛒 Sepette Terk — Kargo ($10) & Teslimat Şoku";
        }
        else
        {
            leakStage = "🚀 Sağlıklı Huni (Bundle & Ölçekleme Hazır)";
        }

        var offerLadder = $"1. Adım (Favori): %{guardrail.RecommendedFavCouponPct} (FAV{guardrail.RecommendedFavCouponPct}) ➔ 2. Adım (Sepet 24s): %{guardrail.RecommendedCartCouponPct} veya Ücretsiz Kargo ➔ Maks. Güvenli Sınır: %{guardrail.MaxSafeCouponStandardPct:0.0} (Offsite Ads ile %{guardrail.MaxSafeCouponWithOffsiteAdsPct:0.0})";

        var shortTitle = productTitle.Length > 28 ? productTitle[..28].Trim() + "..." : productTitle;
        var bundleSuggestion = $"ShipEntegra 0-500gr aynı kargo bareminde '{shortTitle}' yanına +$14.90 tamamlayıcı 3D stand/aksesuar varyantı ekleyin (Ekstra kargo maliyeti $0, Sepet AOV +33%).";

        return (
            intentScore,
            leakStage,
            guardrail.MaxSafeCouponStandardPct,
            guardrail.MaxSafeCouponWithOffsiteAdsPct,
            guardrail.RecommendedFavCouponPct,
            guardrail.RecommendedCartCouponPct,
            offerLadder,
            bundleSuggestion);
    }

    /// <summary>
    /// Mağaza geneli için 3 Kademeli Teklif Merdiveni (Offer Ladder) ve Cosplay/Hediye Etkinlik Takvimi kurallarını üretir.
    /// </summary>
    public static AiCroElasticitySummaryDto BuildStoreCroElasticitySummary(
        decimal avgItemPriceUsd,
        decimal avgShippingUsd,
        decimal avgUnitCostUsd,
        int projectedMonthlyVisits,
        decimal overallCrPct,
        int totalFavorites,
        int totalCarts,
        decimal exchangeRate = 49.50m)
    {
        var guardrail = CalculateCouponGuardrail(
            itemPriceUsd: avgItemPriceUsd > 0m ? avgItemPriceUsd : 45.00m,
            shippingChargedUsd: avgShippingUsd >= 0m ? avgShippingUsd : 10.00m,
            unitTotalCostUsd: avgUnitCostUsd > 0m ? avgUnitCostUsd : 12.50m,
            targetMinProfitUsd: 5.00m,
            activeCouponPct: 30.0m,
            exchangeRate: exchangeRate);

        var scenarios = BuildPriceShippingElasticityMatrix(
            baseItemPriceUsd: guardrail.ItemPriceUsd,
            baseShippingChargedUsd: guardrail.ShippingChargedUsd,
            unitTotalCostUsd: guardrail.UnitTotalCostUsd,
            projectedMonthlyVisits: projectedMonthlyVisits,
            baseConversionRatePct: overallCrPct,
            exchangeRate: exchangeRate);

        var ladderTiers = new List<AiOfferLadderTierDto>
        {
            new(
                TierOrder: 1,
                TierName: "1. Kademe: Soğuk Favori Kancası (Favorited Item Offer)",
                TargetSegment: $"Ürünü favoriye ekleyen {Math.Max(12, totalFavorites)} potansiyel alıcı (Niyet Skoru: 35–59)",
                TriggerTiming: "Favoriye ekledikten 12 saat sonra otomatik",
                CouponCode: $"FAV{guardrail.RecommendedFavCouponPct}",
                DiscountSpec: $"%{guardrail.RecommendedFavCouponPct} İndirim + 3D Ölçek/Kalite Güvencesi",
                ExpectedConversionLiftPct: 14.5m,
                UnitNetProfitAfterOfferUsd: Math.Round(guardrail.ItemPriceUsd * (1m - guardrail.RecommendedFavCouponPct / 100m) + guardrail.ShippingChargedUsd - ((guardrail.ItemPriceUsd * (1m - guardrail.RecommendedFavCouponPct / 100m) + guardrail.ShippingChargedUsd) * DefaultEtsyFeeRatio) - guardrail.UnitTotalCostUsd, 2),
                PsychologicalPrinciple: "Mikro-Teşvik & Hatırlatma: Marjı eritmeden favoriden sepete geçişi tetikler."),

            new(
                TierOrder: 2,
                TierName: "2. Kademe: Sıcak Sepet & Kargo Şoku Kurtarıcı (Abandoned Cart Offer)",
                TargetSegment: $"Sepetinde ürün bekleten {Math.Max(8, totalCarts)} yüksek niyetli alıcı (Niyet Skoru: 60–84)",
                TriggerTiming: "Sepete ekledikten 24 saat sonra ödeme yapılmazsa",
                CouponCode: $"CART{guardrail.RecommendedCartCouponPct}",
                DiscountSpec: $"%{guardrail.RecommendedCartCouponPct} Sepet İndirimi veya Ücretsiz Kargo ($10 Kargo Bariyerini Kaldırır)",
                ExpectedConversionLiftPct: 26.8m,
                UnitNetProfitAfterOfferUsd: Math.Round(guardrail.ItemPriceUsd * (1m - guardrail.RecommendedCartCouponPct / 100m) + guardrail.ShippingChargedUsd - ((guardrail.ItemPriceUsd * (1m - guardrail.RecommendedCartCouponPct / 100m) + guardrail.ShippingChargedUsd) * DefaultEtsyFeeRatio) - guardrail.UnitTotalCostUsd, 2),
                PsychologicalPrinciple: "Kayıptan Kaçınma (Loss Aversion) & Kargo Şoku Çözümü: Müşterinin ödeme adımında gördüğü +$10 kargo acısını nötrler."),

            new(
                TierOrder: 3,
                TierName: "3. Kademe: VIP / Aynı Kargoda 2'li Bundle Teklifi (Thank You & Upsell)",
                TargetSegment: "2+ ürün bakanlar, tekrar müşteriler ve yüksek marjlı Cosplay setleri (Niyet Skoru: 85+)",
                TriggerTiming: "Sipariş sonrası anında veya $75+ sepet büyüklüğünde",
                CouponCode: $"VIP{guardrail.RecommendedVipCouponPct}",
                DiscountSpec: $"%{guardrail.RecommendedVipCouponPct} VIP Kuponu (C_max Korumalı) + 2. Aksesuarda Aynı Kargo",
                ExpectedConversionLiftPct: 34.0m,
                UnitNetProfitAfterOfferUsd: Math.Round((guardrail.ItemPriceUsd * 1.65m) * (1m - guardrail.RecommendedVipCouponPct / 100m) + guardrail.ShippingChargedUsd - (((guardrail.ItemPriceUsd * 1.65m) * (1m - guardrail.RecommendedVipCouponPct / 100m) + guardrail.ShippingChargedUsd) * DefaultEtsyFeeRatio) - (guardrail.UnitTotalCostUsd * 1.35m), 2),
                PsychologicalPrinciple: "Birim Kargo Arbitrajı (Upsell Bundling): 500gr altı pakette 2. ürünü ekstra kargo ödemeden gönderip sepet tutarını (AOV) katlar.")
        };

        var urgencyHooks = new List<AiEventUrgencyHookDto>
        {
            new(
                EventCode: "Q4_HOLIDAY_RUSH",
                EventTitle: "🎄 Q4 / Noel & Yılbaşı Hediye Son Kargo Garantisi",
                TargetAudience: "Arcane / Gaming / Cosplay Hediye Arayanlar",
                UrgencyHookText: "Atölye Kapasite Uyarısı: Yılbaşı teslimat garantisi için bu hafta yalnızca 15 adet 3D baskı slotu açıktır. Bugün verilen siparişler 3 iş günü erken kargolanır!",
                RecommendedAction: "İlan başlığı ve mağaza duyurusuna 'Fast 3-Day 3D Print Dispatch' rozeti ekleyin."),
            new(
                EventCode: "COSPLAY_CONVENTION_SLOT",
                EventTitle: "🎭 Cosplay & Comic-Con Etkinlik Yetiştirme Garantisi",
                TargetAudience: "Hexcore Rubik's Cube, Butterfly Balisong & Ben 10 Omnitrix Koleksiyonerleri",
                UrgencyHookText: "Etkinlik tarihine yetişmesi için ekspres üretim sırası aktif! Şimdi sepetinizi tamamlarsanız ücretsiz sergileme standı (Display Stand) paketinize hediye eklenir.",
                RecommendedAction: "Ücretsiz 15gr 3D sergileme standını ($0.35 filament maliyeti) %30 indirim yerine yüksek değerli hediye çapa olarak sunun.")
        };

        return new AiCroElasticitySummaryDto(
            CouponGuardrail: guardrail,
            ElasticityScenarios: scenarios,
            OfferLadderTiers: ladderTiers,
            EventUrgencyHooks: urgencyHooks);
    }
}
