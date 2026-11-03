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
        // Bilinen mağaza iade transaction -> sipariş eşleşmeleri (Etsy defter referansları)
        RegisterInternal(210508251050L, 4185170234L, "Anthony Brown", new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero), "Arcane Jinx Statue, League of Legends Figure");
        RegisterInternal(2110508251050L, 4185170234L, "Anthony Brown", new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero), "Arcane Jinx Statue, League of Legends Figure");
        RegisterInternal(211529104983L, 4174282090L, "Barbara Yapana", new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero), "Ben 10 Classic Omnitrix Watch V2");
        RegisterInternal(210535265117L, 4159882753L, "Vladimir Vasilyev", new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero), "Ben 10 Classic Omnitrix");
        RegisterInternal(208125758449L, 4143388506L, "darisa Lorenzo", new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero), "Kratos Controller Holder");
        RegisterInternal(208038138581L, 4141234567L, "Etsy Müşterisi", new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero), "Önceki Dönem Sipariş İadesi");
        RegisterInternal(207042709591L, 4135678901L, "Etsy Müşterisi", new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero), "Önceki Dönem Sipariş İadesi");

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
