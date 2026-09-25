namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;

/// <summary>
/// Aras Global canlı fiyat hesaplama isteği modeli.
/// </summary>
public sealed class ArasGlobalQuoteRequest
{
    public string SenderCountry { get; set; } = "TR";
    public string ReceiverCountry { get; set; } = "US";
    public string ReceiverPostalCode { get; set; } = "8537";
    public string ReceiverCity { get; set; } = string.Empty;
    public string ReceiverState { get; set; } = string.Empty;
    public string ReceiverTown { get; set; } = string.Empty;

    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;

    public double ItemUnitPrice { get; set; } = 10.0;
    public string Currency { get; set; } = "USD";
    public bool IsIndividualCustomer { get; set; } = true;
    public int PackageType { get; set; } = 1;

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
/// Aras Global API'sinden veya yerel tarife listesinden dönen kargo teklifi.
/// </summary>
public sealed class ArasGlobalQuoteOffer
{
    public string Cargo { get; set; } = string.Empty; // "Widect", "UPS", "FedEx", "Aras Global"
    public decimal Price { get; set; }
    public decimal UnDiscountedPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public double DiscountRate { get; set; }
    public double EstimatedStartDeliveryDate { get; set; }
    public double EstimatedEndDeliveryDate { get; set; }
    public string ProviderServiceType { get; set; } = string.Empty; // "Eco Express", "Express"
    public bool IsActive { get; set; } = true;
    public bool IsLivePrice { get; set; } = true; // Canlı API'den mi yoksa yedek listeden mi geldi?

    public string DeliveryDaysText =>
        (EstimatedStartDeliveryDate > 0 && EstimatedEndDeliveryDate > 0)
            ? $"{EstimatedStartDeliveryDate:0}-{EstimatedEndDeliveryDate:0} Gün"
            : (EstimatedEndDeliveryDate > 0 ? $"{EstimatedEndDeliveryDate:0} Gün" : "Belirtilmedi");
}

/// <summary>
/// Aras Global kullanıcı ayarları ve oturum tokeni.
/// </summary>
public sealed class ArasGlobalSettings
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
        !string.IsNullOrWhiteSpace(BearerToken) && CleanToken.Length > 20 && !JwtTokenInspector.IsExpired(CleanToken);
}

/// <summary>
/// Aras Global oturum tokeninin süresi dolduğunda veya yetki reddedildiğinde fırlatılan özel istisna.
/// </summary>
public sealed class ArasGlobalTokenExpiredException : Exception
{
    public ArasGlobalTokenExpiredException(string message) : base(message) { }
    public ArasGlobalTokenExpiredException(string message, Exception innerException) : base(message, innerException) { }
}
