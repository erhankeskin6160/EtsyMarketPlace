namespace EtsyMarketPlace.Application.EtsyIntegration;

using System;
using System.Collections.Concurrent;

public sealed record TransactionOrderReference(long ReceiptId, string BuyerName, DateTimeOffset CreatedAt, string Title);

/// <summary>
/// Etsy Open API v3 üzerinden çekilen işlem (transaction_id) ile sipariş (receipt_id)
/// arasındaki eşleştirmeyi hafızada tutan eşzamanlı önbellek.
/// Defter iadelerindeki (REFUND_GROSS) 13 haneli transaction referanslarının
/// sipariş numarası ve müşteri adıyla birebir eşleştirilmesini sağlar.
/// </summary>
public static class EtsyTransactionOrderCache
{
    private static readonly ConcurrentDictionary<long, TransactionOrderReference> Cache = new();

    public static bool TryGetOrderReference(long transactionId, out TransactionOrderReference? reference)
    {
        return Cache.TryGetValue(transactionId, out reference);
    }

    public static void Register(long transactionId, long receiptId, string buyerName, DateTimeOffset createdAt, string title)
    {
        if (transactionId > 0 && receiptId > 0)
        {
            Cache[transactionId] = new TransactionOrderReference(receiptId, buyerName, createdAt, title);
        }
    }

    public static void Clear()
    {
        Cache.Clear();
    }
}
