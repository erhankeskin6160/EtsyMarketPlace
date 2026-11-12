namespace EtsyMarketPlace.Application.EtsyIntegration;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

public sealed record TransactionOrderReference(long ReceiptId, string BuyerName, DateTimeOffset CreatedAt, string Title);

/// <summary>
/// Etsy Open API v3 üzerinden çekilen işlem (transaction_id) ile sipariş (receipt_id)
/// arasındaki eşleştirmeyi hafızada ve diskte tutan eşzamanlı önbellek.
/// Defter iadelerindeki (REFUND_GROSS) 12-13 haneli transaction referanslarının
/// sipariş numarası ve müşteri adıyla birebir eşleştirilmesini ve tekilleştirilmesini sağlar.
/// </summary>
public static class EtsyTransactionOrderCache
{
    private static readonly ConcurrentDictionary<long, TransactionOrderReference> Cache = new();
    private static readonly object FileLock = new();

    static EtsyTransactionOrderCache()
    {
        // Sıfır Mock İlkesi: sabit kodlu eşleşme yok. Kayıtlar yalnızca Etsy API'den (Register)
        // ve diske yazılmış önceki API eşleşmelerinden (LoadFromDisk) gelir.
        LoadFromDisk();
    }

    public static bool TryGetOrderReference(long transactionId, out TransactionOrderReference? reference)
    {
        return Cache.TryGetValue(transactionId, out reference);
    }

    public static void Register(long transactionId, long receiptId, string buyerName, DateTimeOffset createdAt, string title)
    {
        if (transactionId > 0 && receiptId > 0)
        {
            RegisterInternal(transactionId, receiptId, buyerName, createdAt, title);
            SaveToDisk();
        }
    }

    private static void RegisterInternal(long transactionId, long receiptId, string buyerName, DateTimeOffset createdAt, string title)
    {
        Cache[transactionId] = new TransactionOrderReference(receiptId, buyerName, createdAt, title);
    }

    public static void Clear()
    {
        Cache.Clear();
    }

    private static string GetCacheFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "EtsyMarketPlace");
        if (!Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); } catch { }
        }
        return Path.Combine(dir, "transaction_order_cache.json");
    }

    private static void LoadFromDisk()
    {
        try
        {
            var filePath = GetCacheFilePath();
            if (!File.Exists(filePath)) return;

            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, TransactionOrderReference>>(json);
            if (dict != null)
            {
                foreach (var (keyStr, val) in dict)
                {
                    if (long.TryParse(keyStr, out var txId) && val != null)
                    {
                        Cache[txId] = val;
                    }
                }
            }
        }
        catch { }
    }

    private static void SaveToDisk()
    {
        try
        {
            var filePath = GetCacheFilePath();
            lock (FileLock)
            {
                var dict = new Dictionary<string, TransactionOrderReference>();
                foreach (var kvp in Cache)
                {
                    dict[kvp.Key.ToString()] = kvp.Value;
                }
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
        }
        catch { }
    }
}
