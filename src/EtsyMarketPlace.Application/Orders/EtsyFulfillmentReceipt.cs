namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;

/// <summary>
/// Etsy sipariş kuyruğu için canlı API'den gelen hafif sipariş görünümü.
/// Finansal raporlardaki <c>OwnShopReceipt</c> modelinden bağımsızdır; kargo
/// kuyruğunun ihtiyaç duyduğu alanları (adres, IOSS, takip numaraları, satırlar) taşır.
/// </summary>
public sealed record EtsyFulfillmentReceipt(
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
    string IossNumber,
    decimal TotalPrice,
    string Currency,
    DateTimeOffset CreatedAt,
    bool WasShipped,
    IReadOnlyList<string> TrackingCodes,
    IReadOnlyList<EtsyFulfillmentLine> Lines);

/// <summary>Receipt içindeki tek bir ürün satırı.</summary>
public sealed record EtsyFulfillmentLine(
    long ListingId,
    string Title,
    int Quantity,
    decimal UnitPrice);
