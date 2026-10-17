namespace EtsyMarketPlace.Domain.Orders;

using System;

/// <summary>
/// Alıcı teslimat adresinin yerel SQLite veri tabanında kalıcı olarak saklanan modeli.
/// </summary>
public sealed record OrderAddressRecord(
    long ReceiptId,
    string BuyerName,
    string BuyerEmail,
    string Phone,
    string StreetAddress,
    string SecondAddress,
    string City,
    string State,
    string PostalCode,
    string CountryCode,
    string CountryName,
    string Source, // "EtsyCsv", "Clipboard", "Manual", "EtsyApi"
    DateTime UpdatedAtUtc)
{
    public string IossNumber { get; init; } = string.Empty;
    public decimal ProductCostUsd { get; init; } = 0m;
    public decimal ShippingCostUsd { get; init; } = 0m;
    public double WeightKg { get; init; } = 0.0;
    public double WidthCm { get; init; } = 0.0;
    public double LengthCm { get; init; } = 0.0;
    public double HeightCm { get; init; } = 0.0;
    public string GtipCode { get; init; } = string.Empty;
    public string SelectedCarrier { get; init; } = string.Empty;
    public string CarrierServiceName { get; init; } = string.Empty;
    public string TrackingCode { get; init; } = string.Empty;
    public string LabelUrl { get; init; } = string.Empty;
}
