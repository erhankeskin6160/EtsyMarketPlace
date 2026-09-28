namespace EtsyMarketPlace.Domain.Shipping;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Aras Global fiyat hesaplama motorunu başlatma isteği.
/// Alan adları, panel ağ trafiğinden çıkarılan resmi sözleşmeyle birebir aynıdır
/// (küçük harfle başlayan camelCase). Bu uç noktaya yanlış adla gönderilen alanlar
/// (ör. receiverCountryCode) sessizce yok sayılır ve sonraki CreateShipment adımı
/// eksik veriyle çalışır.
/// </summary>
public sealed class ArasStartCalculationPayload
{
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("discountCode")] public string DiscountCode { get; set; } = string.Empty;
    [JsonPropertyName("internationalShipmentCategory")] public string InternationalShipmentCategory { get; set; } = "4";
    [JsonPropertyName("isIndividualCustomer")] public bool IsIndividualCustomer { get; set; } = true;
    [JsonPropertyName("isMicroExport")] public bool IsMicroExport { get; set; } = true;
    [JsonPropertyName("packageCount")] public int PackageCount { get; set; } = 1;
    [JsonPropertyName("packageType")] public int PackageType { get; set; } = 1;
    [JsonPropertyName("receiverCity")] public string ReceiverCity { get; set; } = string.Empty;
    [JsonPropertyName("receiverCountry")] public string ReceiverCountry { get; set; } = "US";
    [JsonPropertyName("receiverPostalCode")] public string ReceiverPostalCode { get; set; } = string.Empty;
    [JsonPropertyName("receiverState")] public string ReceiverState { get; set; } = string.Empty;
    [JsonPropertyName("receiverTown")] public string ReceiverTown { get; set; } = string.Empty;
    [JsonPropertyName("senderCountry")] public string SenderCountry { get; set; } = "TR";

    /// <summary>Hacim ağırlığı (desi). API'nin fiyat bağlamında bulunması için gönderilir.</summary>
    [JsonPropertyName("volumetricWeight")] public double VolumetricWeight { get; set; }

    [JsonPropertyName("desi")] public double Desi { get; set; }

    /// <summary>Yalnızca gönderi oluşturulduktan sonraki fiyat hesabında doldurulur.</summary>
    [JsonPropertyName("shipmentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShipmentId { get; set; }

    [JsonPropertyName("totalPrice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TotalPrice { get; set; }

    [JsonPropertyName("senderPostalCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SenderPostalCode { get; set; }

    [JsonPropertyName("senderCity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SenderCity { get; set; }

    [JsonPropertyName("senderState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SenderState { get; set; }

    [JsonPropertyName("senderTown")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SenderTown { get; set; }

    [JsonPropertyName("shipmentDimensions")] public List<ArasStartCalculationBox> ShipmentDimensions { get; set; } = new();
}

/// <summary>Fiyat başlatma isteğindeki tek kutu (bu adımda desi/hacim ağırlığı GÖNDERİLMEZ).</summary>
public sealed class ArasStartCalculationBox
{
    [JsonPropertyName("length")] public double Length { get; set; } = 20.0;
    [JsonPropertyName("width")] public double Width { get; set; } = 15.0;
    [JsonPropertyName("height")] public double Height { get; set; } = 10.0;
    [JsonPropertyName("weight")] public double Weight { get; set; } = 0.4;
    [JsonPropertyName("volumetricWeight")] public double VolumetricWeight { get; set; }
    [JsonPropertyName("desi")] public double Desi { get; set; }
    [JsonPropertyName("packageCount")] public int PackageCount { get; set; } = 1;

    [JsonPropertyName("shipmentItems")] public List<ArasStartCalculationItem> ShipmentItems { get; set; } = new();
}

/// <summary>Fiyat başlatma isteğindeki kalem (miktar / GTİP / birim fiyat).</summary>
public sealed class ArasStartCalculationItem
{
    [JsonPropertyName("quantity")] public int Quantity { get; set; } = 1;
    [JsonPropertyName("hsCode")] public string HsCode { get; set; } = string.Empty;
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
}
