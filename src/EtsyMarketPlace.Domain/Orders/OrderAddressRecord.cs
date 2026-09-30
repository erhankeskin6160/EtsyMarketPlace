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
    DateTime UpdatedAtUtc);
