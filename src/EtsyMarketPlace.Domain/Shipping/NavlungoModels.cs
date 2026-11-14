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

        // Navlungo Portal Üye Fiyatlarına Tam Kalibre Edilmiş Oranlar:
        // Referans (US 0.60 desi) -> Widect $15.03, FedEx $20.75, UPS Express $34.00, UPS Saver $38.85
        decimal b = (decimal)billable;
        decimal widectPrice = Math.Round((10.50m + (b * 7.55m)) * countryMult, 2, MidpointRounding.AwayFromZero);
        decimal fedexPrice = Math.Round((14.50m + (b * 10.41666666666666666666666667m)) * countryMult, 2, MidpointRounding.AwayFromZero);
        decimal upsExpressPrice = Math.Round((24.00m + (b * 16.66666666666666666666666667m)) * countryMult, 2, MidpointRounding.AwayFromZero);
        decimal upsSaverPrice = Math.Round((27.50m + (b * 18.91666666666666666666666667m)) * countryMult, 2, MidpointRounding.AwayFromZero);

        decimal declared = request.DeclaredValueUsd > 0m ? request.DeclaredValueUsd : 1m;
        decimal taxRate = country == "US" ? 0.178m : 0m;
        decimal taxPortion = Math.Round(declared * taxRate, 2, MidpointRounding.AwayFromZero);

        decimal widectPeak = 2.00m;
        decimal fedexPeak = 1.00m;
        decimal upsExpressPeak = 1.00m;
        decimal upsExpressFuel = Math.Round(Math.Max(0m, upsExpressPrice - upsExpressPeak) * 0.3435m, 2, MidpointRounding.AwayFromZero);
        decimal upsSaverPeak = 1.00m;
        decimal upsSaverFuel = Math.Round(Math.Max(0m, upsSaverPrice - upsSaverPeak) * 0.3437m, 2, MidpointRounding.AwayFromZero);

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
                DdpFeeUsd = Math.Round(1.55m + taxPortion, 2, MidpointRounding.AwayFromZero),
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
                DdpFeeUsd = Math.Round(4.55m + taxPortion, 2, MidpointRounding.AwayFromZero),
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
                DdpFeeUsd = Math.Round(4.55m + taxPortion, 2, MidpointRounding.AwayFromZero),
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
                DdpFeeUsd = Math.Round(4.55m + taxPortion, 2, MidpointRounding.AwayFromZero),
                PremiumTrackingFeeUsd = 5.00m,
                SignatureRequiredFeeUsd = 5.00m,
                EarlyTrackingEnabled = true,
                IsDdpRequired = true,
                Note = "Navlungo Portalı Referans Fiyatı"
            }
        };
    }
}
