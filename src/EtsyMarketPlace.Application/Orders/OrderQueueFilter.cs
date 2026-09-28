namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Sipariş kuyruğunun filtreleme mantığı.
///
/// Neden Application katmanında: bu kural daha önce arayüzün içinde gömülüydü ve
/// test edilemiyordu. "Gönderildi" tanımı tek yerde durmalı; arayüz yalnızca sonucu gösterir.
/// </summary>
public static class OrderQueueFilter
{
    /// <summary>Kuyruktaki durum filtreleri (arayüzdeki çipler).</summary>
    public const string All = "Tümü";
    public const string Pending = "Bekleyen";
    public const string Shipped = "Gönderildi";

    /// <summary>
    /// Bir sipariş "gönderildi" sayılır mı? Etsy hem <c>Shipped</c> hem <c>Fulfilled</c>
    /// durumunu döndürebildiği için ikisi de kabul edilir.
    /// </summary>
    public static bool IsShipped(EtsyOrderFulfillmentItem? order)
    {
        if (order == null)
        {
            return false;
        }

        return string.Equals(order.Status, "Shipped", StringComparison.OrdinalIgnoreCase)
            || string.Equals(order.Status, "Fulfilled", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Duruma göre süzer. Bilinmeyen/boş filtre "Tümü" gibi davranır.</summary>
    public static IReadOnlyList<EtsyOrderFulfillmentItem> ApplyStatus(
        IEnumerable<EtsyOrderFulfillmentItem>? orders,
        string? statusFilter)
    {
        if (orders == null)
        {
            return Array.Empty<EtsyOrderFulfillmentItem>();
        }

        return statusFilter switch
        {
            Pending => orders.Where(o => !IsShipped(o)).ToList(),
            Shipped => orders.Where(IsShipped).ToList(),
            _ => orders.ToList()
        };
    }

    /// <summary>
    /// Filtre çiplerinin sayaçları (ör. "Bekleyen 2"). Arayüz bunu elle hesaplamaz.
    /// </summary>
    public static (int All, int Pending, int Shipped) Counts(IEnumerable<EtsyOrderFulfillmentItem>? orders)
    {
        var list = orders?.ToList() ?? new List<EtsyOrderFulfillmentItem>();
        int shipped = list.Count(IsShipped);
        return (list.Count, list.Count - shipped, shipped);
    }
}
