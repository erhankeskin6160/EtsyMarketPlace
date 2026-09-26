namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Aras Global gönderi adresi modeli (Alıcı, Gönderici ve Fatura adresi).
/// </summary>
public sealed class ArasAddress
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("firstName")] public string FirstName { get; set; } = string.Empty;
    [JsonPropertyName("lastName")] public string LastName { get; set; } = string.Empty;
    [JsonPropertyName("companyName")] public string CompanyName { get; set; } = string.Empty;
    [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;
    [JsonPropertyName("cityName")] public string CityName { get; set; } = string.Empty;
    [JsonPropertyName("districtName")] public string DistrictName { get; set; } = string.Empty;
    [JsonPropertyName("townName")] public string TownName { get; set; } = string.Empty;
    [JsonPropertyName("countryCode")] public string CountryCode { get; set; } = "TR";
    [JsonPropertyName("fromCountryCode")] public string FromCountryCode { get; set; } = "TR";
    [JsonPropertyName("postalCode")] public string PostalCode { get; set; } = string.Empty;
    [JsonPropertyName("phone")] public string Phone { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("taxId")] public string TaxId { get; set; } = string.Empty; // Etsy IOSS IM3720000224 veya TC/Vergi No
    [JsonPropertyName("hasState")] public bool HasState { get; set; } = false;
    [JsonPropertyName("stateCode")] public string StateCode { get; set; } = string.Empty;
    [JsonPropertyName("stateName")] public string StateName { get; set; } = string.Empty;
    [JsonPropertyName("isCommercialAddress")] public bool IsCommercialAddress { get; set; } = false;
    [JsonPropertyName("isResidentialAddress")] public bool IsResidentialAddress { get; set; } = true;
}

/// <summary>
/// Aras Global gönderi koli/kutu ebatları.
/// </summary>
public sealed class ArasBox
{
    [JsonPropertyName("length")] public double Length { get; set; } = 20.0;
    [JsonPropertyName("width")] public double Width { get; set; } = 15.0;
    [JsonPropertyName("height")] public double Height { get; set; } = 10.0;
    [JsonPropertyName("weight")] public double Weight { get; set; } = 0.4;
    [JsonPropertyName("volumetricWeight")] public double VolumetricWeight { get; set; } = 0.6;
    [JsonPropertyName("desi")] public double Desi { get; set; } = 0.6;
    [JsonPropertyName("packageCount")] public int PackageCount { get; set; } = 1;
}

/// <summary>
/// Aras Global gönderi kalem / ürün detayı (HS Code, açıklama, tutar).
/// </summary>
public sealed class ArasShipmentItem
{
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("itemDescription")] public string ItemDescription { get; set; } = string.Empty;
    [JsonPropertyName("hsCode")] public string HsCode { get; set; } = string.Empty; // GTIP Kodu (örn: 3926400000)
    [JsonPropertyName("quantity")] public int Quantity { get; set; } = 1;
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; } = 0m;
    [JsonPropertyName("height")] public double Height { get; set; } = 10.0;
    [JsonPropertyName("width")] public double Width { get; set; } = 15.0;
    [JsonPropertyName("length")] public double Length { get; set; } = 20.0;
    [JsonPropertyName("weight")] public double Weight { get; set; } = 0.4;
    [JsonPropertyName("volumetricWeight")] public double VolumetricWeight { get; set; } = 0.6;
    [JsonPropertyName("desi")] public double Desi { get; set; } = 0.6;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Aras Global gönderi oluşturma ve güncelleme isteği.
/// </summary>
public sealed class ArasCreateShipmentRequest
{
    [JsonPropertyName("shipmentId")] public string ShipmentId { get; set; } = "00000000-0000-0000-0000-000000000000";
    [JsonPropertyName("shipmentDimensions")] public List<ArasBox> ShipmentDimensions { get; set; } = new();
    [JsonPropertyName("boxList")] public List<ArasBox> BoxList { get; set; } = new();
    [JsonPropertyName("shipmentItems")] public List<ArasShipmentItem> ShipmentItems { get; set; } = new();
    [JsonPropertyName("senderAddress")] public ArasAddress SenderAddress { get; set; } = new();
    [JsonPropertyName("senderBillingAddress")] public ArasAddress SenderBillingAddress { get; set; } = new();
    [JsonPropertyName("receiverAddress")] public ArasAddress ReceiverAddress { get; set; } = new();

    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("totalPrice")] public decimal TotalPrice { get; set; }
    [JsonPropertyName("cargoPrice")] public decimal CargoPrice { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("internationalCargoProvider")] public string InternationalCargoProvider { get; set; } = "widect"; // widect, ups, vb.
    [JsonPropertyName("internationalShipmentCategory")] public string InternationalShipmentCategory { get; set; } = "4"; // 4: E-Ticaret / Mikro İhracat
    [JsonPropertyName("isMicroExport")] public bool IsMicroExport { get; set; } = true;
    [JsonPropertyName("packageCount")] public int PackageCount { get; set; } = 1;

    [JsonPropertyName("weight")] public double Weight { get; set; } = 0.4;
    [JsonPropertyName("volumetricWeight")] public double VolumetricWeight { get; set; } = 0.6;
    [JsonPropertyName("desi")] public double Desi { get; set; } = 0.6;
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

