namespace EtsyMarketPlace.Domain.Orders;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Etsy sipariş öğesi (ürün) detayları.
/// </summary>
public sealed class EtsyOrderItem
{
    public long ListingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Price { get; set; } = 0m;
    public string Currency { get; set; } = "USD";
    public string HsCode { get; set; } = string.Empty; // GTIP Kodu (örn: 3926400000)
    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;
    public string ImageUrl { get; set; } = string.Empty;
}

/// <summary>
/// Kargo stüdyosunda işlenecek Etsy siparişi ve alıcı detayları.
/// </summary>
public sealed class EtsyOrderFulfillmentItem
{
    public long ReceiptId { get; set; }
    public string OrderNumber => $"#{ReceiptId}";

    [JsonPropertyName("buyerName")]
    public string BuyerName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? NameAlias
    {
        get => BuyerName;
        set { if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(BuyerName)) BuyerName = value.Trim(); }
    }

    [JsonPropertyName("buyerEmail")]
    public string BuyerEmail { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? EmailAlias
    {
        get => BuyerEmail;
        set { if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(BuyerEmail)) BuyerEmail = value.Trim(); }
    }

    [JsonPropertyName("streetAddress")]
    public string StreetAddress { get; set; } = string.Empty;

    [JsonPropertyName("first_line")]
    public string? FirstLineAlias
    {
        get => StreetAddress;
        set { if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(StreetAddress)) StreetAddress = value.Trim(); }
    }

    [JsonPropertyName("secondAddress")]
    public string SecondAddress { get; set; } = string.Empty;

    [JsonPropertyName("second_line")]
    public string? SecondLineAlias
    {
        get => SecondAddress;
        set { if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(SecondAddress)) SecondAddress = value.Trim(); }
    }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("postalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [JsonPropertyName("zip")]
    public string? ZipAlias
    {
        get => PostalCode;
        set { if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(PostalCode)) PostalCode = value.Trim(); }
    }

    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = "US";

    [JsonPropertyName("country_iso")]
    public string? CountryIsoAlias
    {
        get => CountryCode;
        set { if (!string.IsNullOrWhiteSpace(value)) CountryCode = value.Trim().ToUpperInvariant(); }
    }

    public string CountryName { get; set; } = string.Empty;
    public bool HasState { get; set; } = false;

    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    public bool IsAddressMissing { get; set; } = false;
    public string AddressWarning { get; set; } = string.Empty;

    /// <summary>
    /// Etsy IOSS Numarası (Örn: IM3720000224). AB gümrüklerinde KDV'nin ödendiğini kanıtlar.
    /// </summary>
    public string IossNumber { get; set; } = string.Empty;
    public bool IsVatCollected { get; set; } = false;

    public decimal TotalPrice { get; set; } = 0m;
    public string Currency { get; set; } = "USD";
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Unfulfilled"; // Unfulfilled, Fulfilled, Shipped

    public List<EtsyOrderItem> Items { get; set; } = new();

    // Seçilen kargo, maliyet, paket ölçüleri ve etiket bilgisi
    public decimal ProductCostUsd { get; set; } = 0m;
    public decimal ShippingCostUsd { get; set; } = 0m;
    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;
    public string GtipCode { get; set; } = string.Empty;
    public string SelectedCarrier { get; set; } = string.Empty;
    public string CarrierServiceName { get; set; } = string.Empty;
    public string TrackingCode { get; set; } = string.Empty;
    public string LabelUrl { get; set; } = string.Empty;

    [JsonPropertyName("rawAddressText")]
    public string RawAddressText { get; set; } = string.Empty;

    public string FormattedAddress =>
        $"{StreetAddress}{(string.IsNullOrWhiteSpace(SecondAddress) ? "" : $", {SecondAddress}")}\n{PostalCode} {City}{(string.IsNullOrWhiteSpace(State) ? "" : $", {State}")}\n{CountryName}";

    public double TotalWeightKg
    {
        get
        {
            double sum = 0;
            foreach (var item in Items)
            {
                sum += item.WeightKg * item.Quantity;
            }
            return sum > 0 ? sum : 0.4;
        }
    }
}
