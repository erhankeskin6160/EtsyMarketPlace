namespace EtsyMarketPlace.Domain.Orders;

using System;
using System.Collections.Generic;

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
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string SecondAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "US";
    public string CountryName { get; set; } = string.Empty;
    public bool HasState { get; set; } = false;
    public string Phone { get; set; } = string.Empty;

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

    // Seçilen kargo ve etiket bilgisi
    public string SelectedCarrier { get; set; } = string.Empty;
    public string TrackingCode { get; set; } = string.Empty;
    public string LabelUrl { get; set; } = string.Empty;

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
