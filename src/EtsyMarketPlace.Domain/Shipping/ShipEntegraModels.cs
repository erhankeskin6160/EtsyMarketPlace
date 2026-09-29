namespace EtsyMarketPlace.Domain.Shipping;

using System.Text.Json.Serialization;

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
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? RefreshTokenLastUpdatedUtc { get; set; }
    public bool AutoRefreshEnabled { get; set; } = true;

    [JsonIgnore]

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

    [JsonIgnore]

    public bool HasValidTokenFormat =>
        !string.IsNullOrWhiteSpace(BearerToken) && CleanToken.Length > 20;
}

/// <summary>
/// ShipEntegra sipariş oluşturma isteği (panel sözleşmesi, gerçek trafikten birebir alındı).
/// </summary>
public sealed class ShipEntegraCreateOrderRequest
{
    [JsonPropertyName("rememberSenderAddress")] public bool RememberSenderAddress { get; set; }
    [JsonPropertyName("shipTo")] public ShipEntegraShipTo ShipTo { get; set; } = new();
    [JsonPropertyName("rememberShipToContact")] public bool RememberShipToContact { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("reference")] public string Reference { get; set; } = string.Empty;
    /// <summary>
    /// Gümrük/gönderim tipi: 1=DDP, 2=DDU, 3=IOSS, 4=ShipEntegra IOSS, 5=HMRC, 7=VOEC.
    /// Panel manuel sipariş akışı alanı boş bırakır (istekte yer almaz). ABD için DDP(1)
    /// canlıda doğrulandı; GB/UK için sunucu DDP'yi reddeder (29.09.2026 canlı).
    /// </summary>
    [JsonPropertyName("shippingType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ShippingType { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("products")] public List<ShipEntegraOrderProduct> Products { get; set; } = new();
    [JsonPropertyName("packages")] public List<ShipEntegraOrderPackage> Packages { get; set; } = new();
}

/// <summary>ShipEntegra alıcı adresi (boş opsiyonel alanlar istekte yer almaz).</summary>
public sealed class ShipEntegraShipTo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("address1")] public string Address1 { get; set; } = string.Empty;

    [JsonPropertyName("address2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Address2 { get; set; }

    [JsonPropertyName("city")] public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; set; }

    [JsonPropertyName("zipCode")] public string ZipCode { get; set; } = string.Empty;
    [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }
}

/// <summary>ShipEntegra sipariş kalemi.</summary>
public sealed class ShipEntegraOrderProduct
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("quantity")] public int Quantity { get; set; } = 1;
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("hsCode")] public string HsCode { get; set; } = string.Empty;
}

/// <summary>ShipEntegra koli bilgisi (cm / kg).</summary>
public sealed class ShipEntegraOrderPackage
{
    [JsonPropertyName("packageQuantity")] public int PackageQuantity { get; set; } = 1;
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("length")] public double Length { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
}

/// <summary>ShipEntegra sipariş oluşturma sonucu (sipariş ve kalem kimlikleri).</summary>
public sealed class ShipEntegraOrderResult
{
    public long OrderId { get; set; }
    public List<long> ItemIds { get; set; } = new();
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>
/// ShipEntegra etiket oluşturma isteği (panel sözleşmesi, gerçek trafikten birebir alındı).
/// </summary>
public sealed class ShipEntegraCreateLabelRequest
{
    [JsonPropertyName("specialService")] public string SpecialService { get; set; } = "shipentegra-express";
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("iossNumber")] public string IossNumber { get; set; } = string.Empty;
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("items")] public List<ShipEntegraLabelItem> Items { get; set; } = new();
    [JsonPropertyName("orderId")] public long OrderId { get; set; }
    [JsonPropertyName("serviceType")] public int ServiceType { get; set; } = 1;
    [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;
    [JsonPropertyName("insurance")] public bool Insurance { get; set; }
    [JsonPropertyName("noTracking")] public bool NoTracking { get; set; }
    [JsonPropertyName("verpackg")] public int Verpackg { get; set; } = -1;
}

/// <summary>Etiket isteğindeki tek kalem.</summary>
public sealed class ShipEntegraLabelItem
{
    [JsonPropertyName("itemId")] public long ItemId { get; set; }
    [JsonPropertyName("declaredPrice")] public decimal DeclaredPrice { get; set; }
    [JsonPropertyName("declaredQuantity")] public int DeclaredQuantity { get; set; } = 1;
    [JsonPropertyName("orderId")] public long OrderId { get; set; }
    [JsonPropertyName("errorMessage")] public Dictionary<string, object> ErrorMessage { get; set; } = new();
    [JsonPropertyName("gtip")] public string Gtip { get; set; } = string.Empty;
}

/// <summary>
/// ShipEntegra oturum tokeninin süresi dolduğunda veya yetki reddedildiğinde fırlatılan özel istisna.
/// </summary>
public sealed class ShipEntegraTokenExpiredException : Exception
{
    public ShipEntegraTokenExpiredException(string message) : base(message) { }
    public ShipEntegraTokenExpiredException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// ShipEntegra iş kuralı reddi (örn. ERR.28050.1008: hedef ülke için DDP geçerli değil).
/// Yetki hatası değildir; oturum yenileme akışını tetiklememesi gerekir.
/// </summary>
public sealed class ShipEntegraBusinessException : Exception
{
    public ShipEntegraBusinessException(string code, string description, int statusCode)
        : base(string.IsNullOrWhiteSpace(description) ? code : $"{description} ({code})")
    {
        Code = code ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? Code : description;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public string Description { get; }

    public int StatusCode { get; }
}

/// <summary>
/// ShipEntegra oturum giriş yanıtındaki token çifti (v4.public biçimi).
/// </summary>
public sealed class ShipEntegraAuthTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
