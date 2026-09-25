namespace EtsyMarketPlace.Domain.Shipping;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Kargo oluşturma işlem bağlamı (Context).
/// </summary>
public sealed class ShipmentCreationContext
{
    public EtsyOrderFulfillmentItem Order { get; set; } = new();
    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;
    public string HsCode { get; set; } = string.Empty;
    public string SelectedSubCarrier { get; set; } = "widect"; // widect, ups, vb.
    public string ServiceType { get; set; } = "Eco Express";
    public ArasAddress SenderAddress { get; set; } = new();
}

/// <summary>
/// Kargo oluşturma sonucu (Takip no, etiket URL, ücret dökümü vb.).
/// </summary>
public sealed class ShipmentCreationResult
{
    public bool IsSuccess { get; set; }
    public string ShipmentId { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string LabelUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public ArasShipmentPriceBreakdown? PriceBreakdown { get; set; }
}

/// <summary>
/// Çoklu kargo firmaları için modüler gönderi oluşturma arayüzü.
/// Şu an Aras Global tam aktiftir; ShipEntegra, Navlungo ve Shiptomore API'leri keşfedildikçe bu arayüz üzerinden bağlanacaktır.
/// </summary>
public interface IShipmentCreationProvider
{
    string ProviderName { get; }
    bool IsCreationSupported { get; }
    Task<ShipmentCreationResult> CreateShipmentAsync(ShipmentCreationContext context, CancellationToken cancellationToken = default);
}
