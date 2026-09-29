namespace EtsyMarketPlace.Domain.Shipping;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Ship to More resmî API şemaları (openapi.json v1.1.0).
/// Alan adları şartnamedeki snake_case karşılıklarıyla birebir aynıdır;
/// bu adlar yanlış yazılırsa API alanı sessizce yok sayar.
/// </summary>
public sealed class ShiptomorePriceRequest
{
    [JsonPropertyName("country_code")] public string CountryCode { get; set; } = "US";
    [JsonPropertyName("package_type")] public string PackageType { get; set; } = "custom";
    [JsonPropertyName("billing_weight")] public double? BillingWeight { get; set; }
    [JsonPropertyName("provider_slug")] public string? ProviderSlug { get; set; }
    [JsonPropertyName("parcels")] public List<ShiptomoreParcelDimensions>? Parcels { get; set; }
}

/// <summary>Fiyat hesaplamada kullanılan kutu ölçüsü (faturalama ağırlığını API hesaplar).</summary>
public sealed class ShiptomoreParcelDimensions
{
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("length")] public double Length { get; set; }
    [JsonPropertyName("qty")] public int Qty { get; set; } = 1;
}

/// <summary>Tek bir taşıyıcı fiyat seçeneği.</summary>
public sealed class ShiptomorePriceOption
{
    [JsonPropertyName("provider_slug")] public string ProviderSlug { get; set; } = string.Empty;
    [JsonPropertyName("service_slug")] public string ServiceSlug { get; set; } = string.Empty;
    [JsonPropertyName("price")] public decimal Price { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("billing_weight")] public double BillingWeight { get; set; }
}

public sealed class ShiptomorePriceResponse
{
    [JsonPropertyName("data")] public List<ShiptomorePriceOption> Data { get; set; } = new();
}

/// <summary>Gönderi kalemi (gümrük satırı).</summary>
public sealed class ShiptomoreProductLine
{
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("qty")] public int Qty { get; set; } = 1;
    [JsonPropertyName("unit_price")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("origin_country_code")] public string? OriginCountryCode { get; set; }
    [JsonPropertyName("hs_code")] public string? HsCode { get; set; }
}

/// <summary>Gönderi kutusu. Bu adımda ebat 0 olabilir (yalnız ağırlık zorunlu).</summary>
public sealed class ShiptomoreParcel
{
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("length")] public double Length { get; set; }
    [JsonPropertyName("qty")] public int Qty { get; set; } = 1;
    [JsonPropertyName("box_count")] public int BoxCount { get; set; } = 1;
}

/// <summary>Gönderi oluşturma isteği. Zorunlu alanlar şartnamede işaretlidir.</summary>
public sealed class ShiptomoreShipmentRequest
{
    // --- zorunlu ---
    [JsonPropertyName("provider_slug")] public string ProviderSlug { get; set; } = string.Empty;
    [JsonPropertyName("service_slug")] public string ServiceSlug { get; set; } = string.Empty;
    [JsonPropertyName("receiver_country_code")] public string ReceiverCountryCode { get; set; } = string.Empty;
    [JsonPropertyName("receiver_name")] public string ReceiverName { get; set; } = string.Empty;
    [JsonPropertyName("receiver_phone")] public string ReceiverPhone { get; set; } = string.Empty;
    [JsonPropertyName("receiver_email")] public string ReceiverEmail { get; set; } = string.Empty;
    [JsonPropertyName("receiver_street")] public string ReceiverStreet { get; set; } = string.Empty;
    [JsonPropertyName("receiver_city")] public string ReceiverCity { get; set; } = string.Empty;
    [JsonPropertyName("receiver_zip")] public string ReceiverZip { get; set; } = string.Empty;

    // --- opsiyonel alıcı alanları ---
    [JsonPropertyName("receiver_street2")] public string? ReceiverStreet2 { get; set; }
    [JsonPropertyName("receiver_company_name")] public string? ReceiverCompanyName { get; set; }
    [JsonPropertyName("receiver_state_code")] public string? ReceiverStateCode { get; set; }
    [JsonPropertyName("receiver_national_address")] public string? ReceiverNationalAddress { get; set; }
    [JsonPropertyName("receiver_id_no")] public string? ReceiverIdNo { get; set; }
    [JsonPropertyName("receiver_id_no_type")] public string? ReceiverIdNoType { get; set; }

    /// <summary>Satıcının IOSS numarası. Etsy IOSS'u BURAYA yazılır (receiver_id_no'ya değil).</summary>
    [JsonPropertyName("collect_id_number")] public string? CollectIdNumber { get; set; }
    [JsonPropertyName("collect_id_type")] public string CollectIdType { get; set; } = "ioss";

    /// <summary>"sale" gönderilmezse API varsayılanı "sample" olur ve gönderi numune beyan edilir.</summary>
    [JsonPropertyName("shipment_type")] public string ShipmentType { get; set; } = "sale";
    [JsonPropertyName("package_type")] public string PackageType { get; set; } = "custom";
    [JsonPropertyName("incoterm_code")] public string? IncotermCode { get; set; }

    /// <summary>İzlenebilirlik: Etsy sipariş numarası.</summary>
    [JsonPropertyName("customer_reference")] public string? CustomerReference { get; set; }
    [JsonPropertyName("pickup_date")] public string? PickupDate { get; set; }
    [JsonPropertyName("picking_method_type")] public string? PickingMethodType { get; set; }
    [JsonPropertyName("wants_insurance")] public bool WantsInsurance { get; set; }

    [JsonPropertyName("parcels")] public List<ShiptomoreParcel> Parcels { get; set; } = new();
    [JsonPropertyName("product_lines")] public List<ShiptomoreProductLine> ProductLines { get; set; } = new();

    [JsonPropertyName("save_as_draft")] public bool SaveAsDraft { get; set; }
    [JsonPropertyName("include_labels")] public bool IncludeLabels { get; set; }
    [JsonPropertyName("label_format")] public string LabelFormat { get; set; } = "a4";
}

public sealed class ShiptomoreLabelData
{
    [JsonPropertyName("data")] public string Data { get; set; } = string.Empty;
    [JsonPropertyName("format")] public string Format { get; set; } = "pdf";
}

public sealed class ShiptomoreShipmentResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("odoo_name")] public string OdooName { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("tracking_numbers")] public List<string> TrackingNumbers { get; set; } = new();
    [JsonPropertyName("labels")] public ShiptomoreLabelData? Labels { get; set; }
}

public sealed class ShiptomoreShipmentDetail
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("odoo_name")] public string OdooName { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("tracking_numbers")] public List<string> TrackingNumbers { get; set; } = new();
    [JsonPropertyName("provider_slug")] public string ProviderSlug { get; set; } = string.Empty;
    [JsonPropertyName("service_slug")] public string ServiceSlug { get; set; } = string.Empty;
    [JsonPropertyName("carrier_name")] public string CarrierName { get; set; } = string.Empty;
    [JsonPropertyName("receiver_name")] public string ReceiverName { get; set; } = string.Empty;
    [JsonPropertyName("receiver_country_code")] public string ReceiverCountryCode { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
}

/// <summary>HS/GTIP kodu arama sonucu.</summary>
public sealed class ShiptomoreHsCode
{
    [JsonPropertyName("hs_code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("us_tariff_rate")] public double? UsTariffRate { get; set; }
}
