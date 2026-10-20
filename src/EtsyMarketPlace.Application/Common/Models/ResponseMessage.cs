namespace EtsyMarketPlace.Application.Common.Models;

public static class ResponseMessage
{
    public const string Succeed = "İşlem başarıyla tamamlandı.";
    public const string Failed = "İşlem sırasında bir hata oluştu.";
    public const string NotFound = "Aranan kayıt bulunamadı.";
    public const string ValidationError = "Validasyon kuralları doğrulanamadı.";
    public const string OperationCancelled = "İşlem iptal edildi veya zaman aşımına uğradı.";
    public const string Unauthorized = "Bu işlem için yetkiniz bulunmamaktadır.";
}
