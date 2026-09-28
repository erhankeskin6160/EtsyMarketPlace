namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Aras Global gönderi GÖNDERİCİ / fatura adresi.
/// Alan adları panelin GERÇEK isteğinden birebir alınmıştır (PascalCase sözleşme).
/// </summary>
public sealed class ArasShipmentSenderAddress
{
    [JsonPropertyName("ExternalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExternalId { get; set; }

    [JsonPropertyName("Id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("CityName")] public string CityName { get; set; } = string.Empty;
    [JsonPropertyName("CompanyName")] public string CompanyName { get; set; } = string.Empty;
    [JsonPropertyName("CountryName")] public string CountryName { get; set; } = "Turkiye";
    [JsonPropertyName("CountryCode")] public string CountryCode { get; set; } = "TR";
    [JsonPropertyName("Details")] public string Details { get; set; } = string.Empty;
    [JsonPropertyName("PhoneNumber")] public string PhoneNumber { get; set; } = string.Empty;
    [JsonPropertyName("FirstName")] public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("LastName")] public string LastName { get; set; } = string.Empty;
    [JsonPropertyName("Title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("PostalCode")] public string PostalCode { get; set; } = string.Empty;
    [JsonPropertyName("TownName")] public string TownName { get; set; } = string.Empty;
    [JsonPropertyName("TaxNumber")] public string TaxNumber { get; set; } = string.Empty;
    [JsonPropertyName("Email")] public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Aras Global gönderi ALICI adresi (panel sözleşmesi).
/// </summary>
public sealed class ArasReceiverAddress
{
    [JsonPropertyName("Title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("FirstName")] public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("LastName")] public string LastName { get; set; } = string.Empty;
    [JsonPropertyName("CityName")] public string CityName { get; set; } = string.Empty;
    [JsonPropertyName("TownName")] public string TownName { get; set; } = string.Empty;

    [JsonPropertyName("PhoneCountryCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhoneCountryCode { get; set; }

    [JsonPropertyName("CountryCode")] public string CountryCode { get; set; } = string.Empty;
    [JsonPropertyName("StateCode")] public string StateCode { get; set; } = string.Empty;
    [JsonPropertyName("StateName")] public string StateName { get; set; } = string.Empty;
    [JsonPropertyName("PhoneNumber")] public string PhoneNumber { get; set; } = string.Empty;
    [JsonPropertyName("PostalCode")] public string PostalCode { get; set; } = string.Empty;
    [JsonPropertyName("Email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("CompanyName")] public string CompanyName { get; set; } = string.Empty;
    [JsonPropertyName("Details")] public string Details { get; set; } = string.Empty;
    [JsonPropertyName("Details2")] public string Details2 { get; set; } = string.Empty;
    [JsonPropertyName("Type")] public int Type { get; set; } = 2;
}

/// <summary>Tahmini kutu ölçüleri (panel sözleşmesi).</summary>
public sealed class ArasEstimatedDimensions
{
    [JsonPropertyName("Length")] public double Length { get; set; }
    [JsonPropertyName("Width")] public double Width { get; set; }
    [JsonPropertyName("Height")] public double Height { get; set; }
    [JsonPropertyName("Weight")] public double Weight { get; set; }
}

/// <summary>Gönderi içeriğindeki tek ürün kalemi (panel sözleşmesi).</summary>
public sealed class ArasShipmentContentItem
{
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("HsCode")] public string HsCode { get; set; } = string.Empty;
    [JsonPropertyName("productBarcode")] public string ProductBarcode { get; set; } = string.Empty;
    [JsonPropertyName("disabled")] public bool Disabled { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; } = 1;
    [JsonPropertyName("amount")] public int Amount { get; set; } = 1;
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("manufacturerCountry")] public string ManufacturerCountry { get; set; } = "TR";
    [JsonPropertyName("maxDigitalValue")] public decimal MaxDigitalValue { get; set; }
}

/// <summary>Gönderi içeriği: ölçüler + kalemler (panel sözleşmesi).</summary>
public sealed class ArasShipmentContent
{
    [JsonPropertyName("EstimatedDimensions")] public ArasEstimatedDimensions EstimatedDimensions { get; set; } = new();
    [JsonPropertyName("items")] public List<ArasShipmentContentItem> Items { get; set; } = new();
}

/// <summary>
/// Aras Global gönderi oluşturma (taslak) ve güncelleme isteği.
/// Şema, panelin GERÇEK ağ trafiğinden birebir alınmıştır (PascalCase).
/// "ShipmentId" ve "InternationalCargoProvider" yalnızca UpdateShipment'ta gönderilir;
/// taslak oluşturmada bu alanlar istekte hiç yer almaz.
/// </summary>
public sealed class ArasCreateShipmentRequest
{
    [JsonPropertyName("SenderAddress")] public ArasShipmentSenderAddress SenderAddress { get; set; } = new();
    [JsonPropertyName("BillingAddress")] public object? BillingAddress { get; set; }
    [JsonPropertyName("ReceiverAddress")] public ArasReceiverAddress ReceiverAddress { get; set; } = new();
    [JsonPropertyName("PieceCount")] public int PieceCount { get; set; } = 1;
    [JsonPropertyName("InternationalShipmentCategory")] public string InternationalShipmentCategory { get; set; } = "0";
    [JsonPropertyName("Contents")] public List<ArasShipmentContent> Contents { get; set; } = new();
    [JsonPropertyName("Currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("FINCode")] public string FINCode { get; set; } = string.Empty;
    [JsonPropertyName("PudoPointId")] public string PudoPointId { get; set; } = string.Empty;
    [JsonPropertyName("IsDraftShipment")] public bool IsDraftShipment { get; set; } = true;
    [JsonPropertyName("SaveReceiverAddress")] public bool SaveReceiverAddress { get; set; }
    [JsonPropertyName("SaveBillingAddress")] public bool SaveBillingAddress { get; set; }
    [JsonPropertyName("PackageType")] public int PackageType { get; set; } = 1;
    [JsonPropertyName("SenderBillingAddress")] public ArasShipmentSenderAddress SenderBillingAddress { get; set; } = new();

    [JsonPropertyName("ShipmentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShipmentId { get; set; }

    [JsonPropertyName("InternationalCargoProvider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InternationalCargoProvider { get; set; }
}

/// <summary>
/// Aras Global ek masraf kalemi (Gümrük, Gümrük İşlem, Hizmet Bedeli vb.).
/// </summary>
public sealed class ArasAdditionalServiceItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>
/// Aras Global hesaplanan detaylı masraf ve döviz tablosu.
/// </summary>
public sealed class ArasShipmentPriceBreakdown
{
    public string ShipmentPriceId { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal ExchangeTotalPrice { get; set; }
    public string ExchangeCurrency { get; set; } = "USD";
    public string Currency { get; set; } = "TRY";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal TotalPrice { get; set; }
    public decimal TotalPriceWithProvisionRate { get; set; }
    public List<ArasAdditionalServiceItem> AdditionalServices { get; set; } = new();
}

/// <summary>
/// Gümrük ve DDP/DDU/IOSS opsiyon bilgileri.
/// </summary>
public sealed class ArasAdditionalOptions
{
    public string DefaultMethod { get; set; } = "DDP";
    public bool CustomsFeeRequired { get; set; }
    public bool CustomsProcessFeeRequired { get; set; }
    public List<string> AvailableMethods { get; set; } = new();
}

/// <summary>
/// GTIP / HS Kodu arama sonucu.
/// </summary>
public sealed class ArasGtipSearchResult
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Aras Global gönderi oluşturma / taslak başlatma API yanıt modeli.
/// </summary>
public sealed class ArasCreateShipmentResponse
{
    public bool IsSuccess { get; set; }
    public string ShipmentId { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public int ResultCode { get; set; } = 200;
    public string ResultMessage { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
}
