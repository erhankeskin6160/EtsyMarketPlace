namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Canlı Etsy siparişlerinin kargo kuyruğuna girme politikası.
///
/// Kural: sipariş tarihinden itibaren 45 gün içindeki VE "gönderilmemiş veya
/// takip numarası olmayan" siparişler kuyruğa alınır. Eski siparişler kuyruğu
/// şişirmesin diye 45 günlük pencere dışındakiler atlanır.
/// </summary>
public static class EtsyFulfillmentQueuePolicy
{
    /// <summary>Kuyruğa alınacak siparişler için geriye dönük gün penceresi.</summary>
    public const int LookbackDays = 45;

    /// <summary>Sipariş kuyruğa alınmalı mı?</summary>
    public static bool ShouldInclude(EtsyFulfillmentReceipt receipt, DateTimeOffset now)
    {
        if (receipt == null)
        {
            return false;
        }

        if (receipt.CreatedAt < now.AddDays(-LookbackDays))
        {
            return false;
        }

        return !receipt.WasShipped || !HasTracking(receipt);
    }

    /// <summary>Siparişte geçerli bir takip numarası var mı?</summary>
    public static bool HasTracking(EtsyFulfillmentReceipt receipt)
        => receipt.TrackingCodes.Any(code => !string.IsNullOrWhiteSpace(code));

    /// <summary>Canlı siparişi arayüzün kullandığı kuyruk öğesine çevirir.</summary>
    public static EtsyOrderFulfillmentItem ToQueueItem(EtsyFulfillmentReceipt receipt)
    {
        var item = new EtsyOrderFulfillmentItem
        {
            ReceiptId = receipt.ReceiptId,
            BuyerName = receipt.BuyerName,
            BuyerEmail = receipt.BuyerEmail,
            Phone = receipt.Phone,
            StreetAddress = receipt.StreetAddress,
            SecondAddress = receipt.SecondAddress,
            City = receipt.City,
            State = receipt.State,
            HasState = !string.IsNullOrWhiteSpace(receipt.State),
            PostalCode = receipt.PostalCode,
            CountryCode = string.IsNullOrWhiteSpace(receipt.CountryCode) ? "US" : receipt.CountryCode.ToUpperInvariant(),
            CountryName = string.IsNullOrWhiteSpace(receipt.CountryName) ? receipt.CountryCode : receipt.CountryName,
            IossNumber = receipt.IossNumber,
            IsVatCollected = !string.IsNullOrWhiteSpace(receipt.IossNumber),
            TotalPrice = receipt.TotalPrice,
            Currency = string.IsNullOrWhiteSpace(receipt.Currency) ? "USD" : receipt.Currency,
            OrderDate = receipt.CreatedAt.UtcDateTime,
            Status = "Unfulfilled"
        };

        foreach (var line in receipt.Lines)
        {
            item.Items.Add(new EtsyOrderItem
            {
                ListingId = line.ListingId,
                Title = line.Title,
                Quantity = line.Quantity,
                Price = line.UnitPrice,
                Currency = item.Currency
            });
        }

        return item;
    }

    /// <summary>ISO ülke kodundan İngilizce ülke adı üretir (ör. DE -> Germany).</summary>
    public static string CountryDisplayName(string? isoCode)
    {
        string code = (isoCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }
}
