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
    [JsonPropertyName("shipmentDimensions")] public List<ArasStartCalculationBox> ShipmentDimensions { get; set; } = new();
}

/// <summary>Fiyat başlatma isteğindeki tek kutu (bu adımda desi/hacim ağırlığı GÖNDERİLMEZ).</summary>
public sealed class ArasStartCalculationBox
{
    [JsonPropertyName("length")] public double Length { get; set; } = 20.0;
    [JsonPropertyName("width")] public double Width { get; set; } = 15.0;
    [JsonPropertyName("height")] public double Height { get; set; } = 10.0;
    [JsonPropertyName("weight")] public double Weight { get; set; } = 0.4;
    [JsonPropertyName("packageCount")] public int PackageCount { get; set; } = 1;
}
