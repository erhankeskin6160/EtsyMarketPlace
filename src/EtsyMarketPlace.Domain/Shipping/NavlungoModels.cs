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
