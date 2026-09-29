namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// Gönderisi oluşup etiketi alınamayan siparişler için bekleyen etiket kaydı.
/// Kullanıcı 'Etiketi Önizle' ile yeniden denediğinde aynı istek tekrar gönderilir;
/// böylece sipariş çoğaltılmadan etiket tamamlanır.
/// </summary>
public static class ShipEntegraPendingLabelStore
{
    private static string BaseDir(string? baseDir = null)
        => baseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimilarProductsWinForms",
            "pending-labels");

    private static string PathFor(long receiptId, string? baseDir = null)
        => Path.Combine(BaseDir(baseDir), $"shipentegra-label-{receiptId}.json");

    public static void Save(long receiptId, long orderId, string requestJson, string? serviceCode, string? baseDir = null)
    {
        try
        {
            string dir = BaseDir(baseDir);
            Directory.CreateDirectory(dir);
            var payload = new PendingLabel
            {
                ReceiptId = receiptId,
                OrderId = orderId,
                ServiceCode = serviceCode ?? string.Empty,
                RequestJson = requestJson,
                CreatedAt = DateTime.UtcNow
            };
            File.WriteAllText(PathFor(receiptId, baseDir), JsonSerializer.Serialize(payload));
        }
        catch (Exception)
        {
            // Bekleyen kayıt yazılamazsa etiket panelden de alınabilir; akışı bloklamaz.
        }
    }

    public static bool TryLoad(long receiptId, out long orderId, out string requestJson, string? baseDir = null)
    {
        orderId = 0;
        requestJson = string.Empty;
        try
        {
            string path = PathFor(receiptId, baseDir);
            if (!File.Exists(path))
            {
                return false;
            }

            var payload = JsonSerializer.Deserialize<PendingLabel>(File.ReadAllText(path));
            if (payload == null || payload.OrderId <= 0 || string.IsNullOrWhiteSpace(payload.RequestJson))
            {
                return false;
            }

            orderId = payload.OrderId;
            requestJson = payload.RequestJson;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void Delete(long receiptId, string? baseDir = null)
    {
        try
        {
            string path = PathFor(receiptId, baseDir);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Silinemezse sonraki deneme yine çalışır.
        }
    }

    private sealed class PendingLabel
    {
        public long ReceiptId { get; set; }
        public long OrderId { get; set; }
        public string ServiceCode { get; set; } = string.Empty;
        public string RequestJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
