namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;

/// <summary>
/// Aras Global gönderi adresi modeli (Alıcı, Gönderici ve Fatura adresi).
/// </summary>
public sealed class ArasAddress
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string TownName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "TR";
    public string FromCountryCode { get; set; } = "TR";
    public string PostalCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty; // Etsy IOSS IM3720000224 veya TC/Vergi No
    public bool HasState { get; set; } = false;
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public bool IsCommercialAddress { get; set; } = false;
    public bool IsResidentialAddress { get; set; } = true;
}

/// <summary>
/// Aras Global gönderi koli/kutu ebatları.
/// </summary>
public sealed class ArasBox
{
    public double Length { get; set; } = 20.0;
    public double Width { get; set; } = 15.0;
    public double Height { get; set; } = 10.0;
    public double Weight { get; set; } = 0.4;
    public double VolumetricWeight { get; set; } = 0.6;
    public double Desi { get; set; } = 0.6;
    public int PackageCount { get; set; } = 1;
}

/// <summary>
/// Aras Global gönderi kalem / ürün detayı (HS Code, açıklama, tutar).
/// </summary>
public sealed class ArasShipmentItem
{
    public string Description { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string HsCode { get; set; } = string.Empty; // GTIP Kodu (örn: 3926400000)
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0m;
    public double Height { get; set; } = 10.0;
    public double Width { get; set; } = 15.0;
    public double Length { get; set; } = 20.0;
    public double Weight { get; set; } = 0.4;
    public double VolumetricWeight { get; set; } = 0.6;
    public double Desi { get; set; } = 0.6;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Aras Global gönderi oluşturma ve güncelleme isteği.
/// </summary>
public sealed class ArasCreateShipmentRequest
{
    public string ShipmentId { get; set; } = string.Empty;
    public List<ArasBox> ShipmentDimensions { get; set; } = new();
    public List<ArasBox> BoxList { get; set; } = new();
    public List<ArasShipmentItem> ShipmentItems { get; set; } = new();
    public ArasAddress SenderAddress { get; set; } = new();
    public ArasAddress SenderBillingAddress { get; set; } = new();
    public ArasAddress ReceiverAddress { get; set; } = new();

    public decimal Price { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal CargoPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string InternationalCargoProvider { get; set; } = "widect"; // widect, ups, vb.
    public string InternationalShipmentCategory { get; set; } = "4"; // 4: E-Ticaret / Mikro İhracat
    public bool IsMicroExport { get; set; } = true;
    public int PackageCount { get; set; } = 1;

    public double Weight { get; set; } = 0.4;
    public double VolumetricWeight { get; set; } = 0.6;
    public double Desi { get; set; } = 0.6;
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

