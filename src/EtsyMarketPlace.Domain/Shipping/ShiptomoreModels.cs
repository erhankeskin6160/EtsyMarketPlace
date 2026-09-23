namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;

/// <summary>
/// Shiptomore fiyat hesaplama istek parametreleri.
/// </summary>
public sealed class ShiptomoreQuoteRequest
{
    public string FromCountry { get; set; } = "TR";
    public string ToCountry { get; set; } = "US";
    public double WeightKg { get; set; } = 0.4;
    public double LengthCm { get; set; } = 20;
    public double WidthCm { get; set; } = 15;
    public double HeightCm { get; set; } = 10;
    public int Quantity { get; set; } = 1;
    public string PackageType { get; set; } = "custom";

    public double VolumetricDesi => (LengthCm * WidthCm * HeightCm) / 5000.0;
    public double BillableWeightKg => Math.Max(WeightKg, VolumetricDesi);
}

/// <summary>
/// Shiptomore canlı fiyat teklifi modeli (Widect, FedEx, UPS vb.).
/// </summary>
public sealed class ShiptomoreQuoteOffer
{
    public int ProviderId { get; set; }
    public string Carrier { get; set; } = string.Empty; // "Widect", "FedEx", "UPS"
    public string ServiceName { get; set; } = string.Empty; // "by THY", "Express - 3 Gün"
    public string ServiceDescription { get; set; } = string.Empty; // "4-9 İş Günü"
    public string DeliveryEstimate { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string CurrencySymbol { get; set; } = "$";
    public double BillableWeight { get; set; }
    public string? ProviderLogoUrl { get; set; }
    public bool IsMemberRate { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Shiptomore oturum, kimlik bilgileri ve çerez yapılandırma modeli.
/// </summary>
public sealed class ShiptomoreSettings
{
    public string? SessionCookie { get; set; }
    public string? SavedEmail { get; set; }
    public string? EncryptedPassword { get; set; }
    public DateTime? TokenLastUpdatedUtc { get; set; }
    public bool AutoRefreshEnabled { get; set; } = true;
}
