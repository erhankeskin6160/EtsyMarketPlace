namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Domain.Orders;

/// <summary>Sipariş kuyruğunun sıralama seçenekleri (arayüzdeki dropdown).</summary>
public enum OrderQueueSort
{
    NewestFirst = 0,
    OldestFirst = 1,
    CustomerAscending = 2,
    CustomerDescending = 3
}

/// <summary>
/// Sipariş kuyruğunun arama + sıralama mantığı. Arayüzden bağımsız ve test edilebilir.
///
/// Arama; müşteri adı, sipariş numarası, ürün adı, e-posta ve ülke alanlarında
/// büyük/küçük harf duyarsız çalışır. Varsayılan sıralama "en son gelen sipariş üstte"
/// kuralıdır; kullanıcı dropdown'dan değiştirebilir.
/// </summary>
public static class OrderQueueView
{
    /// <summary>Kuyruğu verilen aramaya göre süzer ve seçilen sıralamaya dizer.</summary>
    public static List<EtsyOrderFulfillmentItem> FilterAndSort(
        IEnumerable<EtsyOrderFulfillmentItem>? orders,
        string? query,
        OrderQueueSort sort)
    {
        var list = orders?.ToList() ?? new List<EtsyOrderFulfillmentItem>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            string q = query.Trim();
            list = list.Where(o => Matches(o, q)).ToList();
        }

        return sort switch
        {
            OrderQueueSort.OldestFirst => list
                .OrderBy(o => o.OrderDate)
                .ThenBy(o => o.ReceiptId)
                .ToList(),
            OrderQueueSort.CustomerAscending => list
                .OrderBy(o => o.BuyerName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(o => o.ReceiptId)
                .ToList(),
            OrderQueueSort.CustomerDescending => list
                .OrderByDescending(o => o.BuyerName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(o => o.ReceiptId)
                .ToList(),
            _ => list
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.ReceiptId)
                .ToList()
        };
    }

    private static bool Matches(EtsyOrderFulfillmentItem order, string query)
    {
        if (Contains(order.BuyerName, query)
            || order.ReceiptId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
            || Contains(order.CountryName, query)
            || Contains(order.CountryCode, query)
            || Contains(order.BuyerEmail, query))
        {
            return true;
        }

        return order.Items.Any(item => Contains(item.Title, query));
    }

    private static bool Contains(string? value, string query)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Ordinal karşılaştırma "inge" -> "Inge" gibi ASCII büyük/küçük harf
        // eşleşmelerini kültürden bağımsız yakalar; kültür karşılaştırması ise
        // Türkçe İ/ı gibi yerel eşleşmeleri kapsar. İkisi birlikte kullanılır.
        return value.Contains(query, StringComparison.OrdinalIgnoreCase)
            || value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
