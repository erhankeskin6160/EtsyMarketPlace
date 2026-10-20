namespace EtsyMarketPlace.Domain.Entities;

using System;

/// <summary>
/// Sipariş alıcı teslimat adresi, paket ölçüleri ve kargo takip bilgileri (PostgreSQL Entity).
/// </summary>
public class OrderAddress
{
    public long ReceiptId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string SecondAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public string IossNumber { get; set; } = string.Empty;
    public decimal ProductCostUsd { get; set; }
    public decimal ShippingCostUsd { get; set; }
    public double WeightKg { get; set; }
    public double WidthCm { get; set; }
    public double LengthCm { get; set; }
    public double HeightCm { get; set; }
    public string GtipCode { get; set; } = string.Empty;
    public string SelectedCarrier { get; set; } = string.Empty;
    public string CarrierServiceName { get; set; } = string.Empty;
    public string TrackingCode { get; set; } = string.Empty;
    public string LabelUrl { get; set; } = string.Empty;
}
