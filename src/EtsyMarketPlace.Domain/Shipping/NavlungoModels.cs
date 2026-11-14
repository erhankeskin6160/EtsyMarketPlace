namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;

/// <summary>
/// Navlungo fiyat hesaplama istek parametreleri.
/// </summary>
public sealed class NavlungoQuoteRequest
{
    public string FromCountry { get; set; } = "TR";
    public string ToCountry { get; set; } = "US";
    public double WeightKg { get; set; } = 0.4;
    public double LengthCm { get; set; } = 20;
    public double WidthCm { get; set; } = 15;
    public double HeightCm { get; set; } = 10;
    public string Source { get; set; } = "user";
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ShipmentId { get; set; } = string.Empty;
    public decimal DeclaredValueUsd { get; set; } = 1m;

    public double VolumetricDesi => (LengthCm * WidthCm * HeightCm) / 5000.0;
    public double BillableWeightKg => Math.Max(WeightKg, VolumetricDesi);
}

/// <summary>
/// Navlungo canlı fiyat teklifi modeli (Widect, FedEx, UPS, DHL vb.).
/// </summary>
public sealed class NavlungoQuoteOffer
{
    public string Carrier { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = "Express"; // "Express", "Ekonomi"
    public string DeliveryEstimate { get; set; } = string.Empty; // "1-3 iş günü", "3-7 iş günü"
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsBestExpress { get; set; }
    public bool IsBestEconomy { get; set; }
    public string Note { get; set; } = string.Empty;

    // Navlungo Resmi Quote API (/api/shipments/{id}/quotes) & Kalem Kırılımı
    public string QuoteId { get; set; } = string.Empty;
    public string ShipmentId { get; set; } = string.Empty;
    public string SearchId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public double ChargeableWeightKg { get; set; }
    public double VolumetricWeightKg { get; set; }
    public decimal BaseSystemOfferUsd { get; set; }
    public decimal FuelSurchargeUsd { get; set; }
    public decimal PeakSeasonSurchargeUsd { get; set; }
    public decimal DdpFeeUsd { get; set; }
    public decimal InsuranceFeeUsd { get; set; }
    public decimal PremiumTrackingFeeUsd { get; set; }
    public decimal SignatureRequiredFeeUsd { get; set; }
    public decimal TryEquivalentAmount { get; set; }
    public bool EarlyTrackingEnabled { get; set; }
    public bool IsDdpRequired { get; set; }
    public string QuoteDescription { get; set; } = string.Empty;

    public decimal DdpIncludedTotalUsd => decimal.Round(Price + DdpFeeUsd, 2);
}

/// <summary>
/// Navlungo oturum, kimlik bilgileri ve çerez yapılandırma modeli.
/// </summary>
public sealed class NavlungoSettings
{
    public string? IdToken { get; set; }
    public string? SessionCookie { get; set; }
    public string? SavedEmail { get; set; }
    public string? EncryptedPassword { get; set; }
    public DateTime? TokenLastUpdatedUtc { get; set; }
    public bool AutoRefreshEnabled { get; set; } = true;
}

/// <summary>
/// Çevrimdışı veya API erişilemediğinde Navlungo Portal tarifesine kalibre edilmiş
/// tek kaynaklı yedek fiyat ve kalem kırılımı hesaplayıcısı.
/// </summary>
public static class NavlungoFallbackPricing
{
    public static List<NavlungoQuoteOffer> GenerateRealisticQuotes(NavlungoQuoteRequest request)
    {
        double billable = request.BillableWeightKg;
        string country = (request.ToCountry ?? "US").Trim().ToUpperInvariant();
        decimal countryMult = country switch
        {
            "CA" => 1.08m,
            "DE" or "FR" or "IT" or "ES" or "NL" or "BE" or "AT" or "PL" or "SE" or "DK" or "FI" or "GB" or "UK" => 0.92m,
            _ => 1.00m
        };

        // ship.navlungo.com Üye Portalı (api.navlungo.com) Canlı Fiyatlarına Kalibre Edilmiş Tarife:
        // 0.60 desi (0.4kg 20x15x10): Widect $16.03 (+$0.67 DDP = $16.70), FedEx $21.25 (+$4.68 = $25.93),
        //                             UPS Express $34.84 ($11.80 yakıt, +$4.68 = $39.52), UPS Expedited $39.73 ($13.48 yakıt, +$4.68 = $44.41)
        // 1.60 desi (1.0kg 25x16x20): Widect $23.20 (+$1.68 DDP = $24.88), FedEx $25.64 (+$4.68 = $30.32),
        //                             UPS Express $44.46 ($14.93 yakıt, +$4.68 = $49.14), UPS Expedited $48.48 ($16.32 yakıt, +$4.68 = $53.16)
        decimal b = (decimal)billable;
        decimal widectPrice = Math.Round((11.728m + (b * 7.17m)) * countryMult, 2, MidpointRounding.AwayFromZero);
        decimal fedexPrice = Math.Round((18.616m + (b * 4.39m)) * countryMult, 2, MidpointRounding.AwayFromZero);
        decimal upsExpressPrice = Math.Round((29.068m + (b * 9.62m)) * countryMult, 2, MidpointRounding.AwayFromZero);
        decimal upsSaverPrice = Math.Round((34.48m + (b * 8.75m)) * countryMult, 2, MidpointRounding.AwayFromZero);

        decimal declared = request.DeclaredValueUsd > 0m ? request.DeclaredValueUsd : 1m;
        decimal taxPortion = country == "US"
            ? Math.Round(declared * 0.053m, 2, MidpointRounding.AwayFromZero) +
              Math.Round(declared * 0.125m, 2, MidpointRounding.AwayFromZero)
            : 0m;
        decimal widectDdpService = b < 1.0m ? 0.49m : 1.50m;
        decimal expressDdpService = 4.50m;

        decimal widectPeak = 2.00m;
        decimal fedexPeak = 1.00m;
        decimal upsExpressPeak = 1.00m;
        decimal upsExpressFuel = Math.Min(
            Math.Max(0m, upsExpressPrice - upsExpressPeak),
            Math.Round((9.922m + (b * 3.13m)) * countryMult, 2, MidpointRounding.AwayFromZero));
        decimal upsSaverPeak = 1.00m;
        decimal upsSaverFuel = Math.Min(
            Math.Max(0m, upsSaverPrice - upsSaverPeak),
            Math.Round((11.776m + (b * 2.84m)) * countryMult, 2, MidpointRounding.AwayFromZero));

        return new List<NavlungoQuoteOffer>
        {
            new()
            {
                Carrier = "Widect",
                CustomerName = "THY Widect Amazon Shipping",
                ServiceName = "Widect 3-7 iş günü",
                ServiceType = "Ekonomi",
                DeliveryEstimate = "3-7 iş günü",
                Price = widectPrice,
                Currency = "USD",
                IsBestEconomy = true,
                ChargeableWeightKg = billable,
                VolumetricWeightKg = request.VolumetricDesi,
                BaseSystemOfferUsd = Math.Max(0m, Math.Round(widectPrice - widectPeak, 2, MidpointRounding.AwayFromZero)),
                FuelSurchargeUsd = 0m,
                PeakSeasonSurchargeUsd = widectPeak,
                DdpFeeUsd = Math.Round(widectDdpService + taxPortion, 2, MidpointRounding.AwayFromZero),
                InsuranceFeeUsd = 3.00m,
                EarlyTrackingEnabled = true,
                IsDdpRequired = true,
                Note = "Navlungo Portalı Referans Fiyatı"
            },
            new()
            {
                Carrier = "FedEx",
                CustomerName = "Federal Express Express",
                ServiceName = "FedEx 1-3 iş günü",
                ServiceType = "Express",
                DeliveryEstimate = "1-3 iş günü",
                Price = fedexPrice,
                Currency = "USD",
                IsBestExpress = true,
                ChargeableWeightKg = billable,
                VolumetricWeightKg = request.VolumetricDesi,
                BaseSystemOfferUsd = Math.Max(0m, Math.Round(fedexPrice - fedexPeak, 2, MidpointRounding.AwayFromZero)),
                FuelSurchargeUsd = 0m,
                PeakSeasonSurchargeUsd = fedexPeak,
                DdpFeeUsd = Math.Round(expressDdpService + taxPortion, 2, MidpointRounding.AwayFromZero),
                PremiumTrackingFeeUsd = 5.00m,
                SignatureRequiredFeeUsd = 5.00m,
                EarlyTrackingEnabled = true,
                IsDdpRequired = true,
                Note = "Navlungo Portalı Referans Fiyatı"
            },
            new()
            {
                Carrier = "UPS",
                CustomerName = "United Parcel Service Express",
                ServiceName = "UPS 1-3 iş günü",
                ServiceType = "Express",
                DeliveryEstimate = "1-3 iş günü",
                Price = upsExpressPrice,
                Currency = "USD",
                ChargeableWeightKg = billable,
                VolumetricWeightKg = request.VolumetricDesi,
                BaseSystemOfferUsd = Math.Max(0m, Math.Round(upsExpressPrice - upsExpressPeak - upsExpressFuel, 2, MidpointRounding.AwayFromZero)),
                FuelSurchargeUsd = upsExpressFuel,
                PeakSeasonSurchargeUsd = upsExpressPeak,
                DdpFeeUsd = Math.Round(expressDdpService + taxPortion, 2, MidpointRounding.AwayFromZero),
                PremiumTrackingFeeUsd = 5.00m,
                SignatureRequiredFeeUsd = 5.00m,
                EarlyTrackingEnabled = true,
                IsDdpRequired = true,
                Note = "Navlungo Portalı Referans Fiyatı"
            },
            new()
            {
                Carrier = "UPS",
                CustomerName = "United Parcel Service Expedited",
                ServiceName = "UPS 2-5 iş günü",
                ServiceType = "Express Saver",
                DeliveryEstimate = "2-5 iş günü",
                Price = upsSaverPrice,
                Currency = "USD",
                ChargeableWeightKg = billable,
                VolumetricWeightKg = request.VolumetricDesi,
                BaseSystemOfferUsd = Math.Max(0m, Math.Round(upsSaverPrice - upsSaverPeak - upsSaverFuel, 2, MidpointRounding.AwayFromZero)),
                FuelSurchargeUsd = upsSaverFuel,
                PeakSeasonSurchargeUsd = upsSaverPeak,
                DdpFeeUsd = Math.Round(expressDdpService + taxPortion, 2, MidpointRounding.AwayFromZero),
                PremiumTrackingFeeUsd = 5.00m,
                SignatureRequiredFeeUsd = 5.00m,
                EarlyTrackingEnabled = true,
                IsDdpRequired = true,
                Note = "Navlungo Portalı Referans Fiyatı"
            }
        };
    }
}
