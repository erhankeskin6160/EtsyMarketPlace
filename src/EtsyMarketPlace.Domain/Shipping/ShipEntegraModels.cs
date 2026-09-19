namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// ShipEntegra canlı fiyat hesaplama isteği modeli.
/// </summary>
public sealed class ShipEntegraQuoteRequest
{
    public string SenderCountry { get; set; } = "TR";
    public string ReceiverCountry { get; set; } = "US";
    public string ReceiverPostalCode { get; set; } = "8537";

    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;

    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Hacimsel Desi hesabı: (En x Boy x Yükseklik) / 5000
    /// </summary>
    public double CalculatedDesi => ComputeDesi(WidthCm, LengthCm, HeightCm);

    /// <summary>
    /// Faturalandırmaya esas ağırlık: max(Gerçek Ağırlık, Desi)
    /// </summary>
    public double BillableWeightKg => ComputeBillableWeight(WeightKg, WidthCm, LengthCm, HeightCm);

    public static double ComputeDesi(double widthCm, double lengthCm, double heightCm) =>
        Math.Round((widthCm * lengthCm * heightCm) / 5000.0, 2);

    public static double ComputeBillableWeight(double weightKg, double widthCm, double lengthCm, double heightCm) =>
        Math.Max(weightKg, ComputeDesi(widthCm, lengthCm, heightCm));
}

/// <summary>
/// ShipEntegra API'sinden veya yerel tarife listesinden dönen kargo teklifi.
/// </summary>
public sealed class ShipEntegraQuoteOffer
{
    public string ServiceName { get; set; } = string.Empty; // "shipentegra-amerika-eko-plus"
    public string ClearServiceName { get; set; } = string.Empty; // "ShipEntegra Amerika Eko Plus"
    public string ServiceType { get; set; } = "ECO"; // "ECO" veya "EXPRESS"
    public decimal CargoPrice { get; set; }
    public decimal FuelCost { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string AdditionalDescription { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
    public bool IsBestCarrier { get; set; }
    public bool IsLivePrice { get; set; } = true;

    /// <summary>
    /// AdditionalDescription içerisinden "3-6 iş günü" veya benzeri teslimat süresini çıkarır.
    /// </summary>
    public string DeliveryDaysText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AdditionalDescription)) return "Belirtilmedi";
            var match = Regex.Match(AdditionalDescription, @"Tahmini Teslim Süresi\s*([0-9\-]+\s*iş günü)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            return AdditionalDescription.Replace("<br>", " ").Trim();
        }
    }
}

/// <summary>
/// ShipEntegra kullanıcı ayarları ve oturum tokeni.
/// </summary>
public sealed class ShipEntegraSettings
{
    public string BearerToken { get; set; } = string.Empty;
    public DateTime? TokenLastUpdatedUtc { get; set; }
    public double DefaultWeightKg { get; set; } = 0.4;
    public double DefaultWidthCm { get; set; } = 15.0;
    public double DefaultLengthCm { get; set; } = 20.0;
    public double DefaultHeightCm { get; set; } = 10.0;
    public string DefaultCountry { get; set; } = "US";
    public string DefaultPostalCode { get; set; } = "8537";

    // Otomatik Oturum & Token Yenileme (Kimlik Bilgileri)
    public string SavedEmail { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public bool AutoRefreshEnabled { get; set; } = true;

    public string CleanToken
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BearerToken)) return string.Empty;
            string t = BearerToken.Trim();
            if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring(7).Trim();
            }
            return t;
        }
    }

    public bool HasValidTokenFormat =>
        !string.IsNullOrWhiteSpace(BearerToken) && CleanToken.Length > 20;
}

/// <summary>
/// ShipEntegra oturum tokeninin süresi dolduğunda veya yetki reddedildiğinde fırlatılan özel istisna.
/// </summary>
public sealed class ShipEntegraTokenExpiredException : Exception
{
    public ShipEntegraTokenExpiredException(string message) : base(message) { }
    public ShipEntegraTokenExpiredException(string message, Exception innerException) : base(message, innerException) { }
}
