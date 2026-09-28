namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Bir gönderi oluşturulduktan sonra yapılabilecek işlemler: etiketi indirmek ve gönderiyi iptal etmek.
///
/// Kullanıcıya dönen metinler Türkçe ve teknik jargon içermez; iptal edilemeyen durumlar
/// (taşıyıcıya teslim edilmiş gönderiler) ayrıca açıklanır.
/// </summary>
public sealed class ShiptomoreShipmentActions
{
    private readonly IShiptomoreOfficialApi _api;

    public ShiptomoreShipmentActions(IShiptomoreOfficialApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <summary>Kimlik bilgisi var mı? Yoksa işlemler kullanıcıya "bağlantı yok" demeli.</summary>
    public bool IsConfigured => _api.HasCredentials;

    /// <summary>
    /// Etiketi indirir. <paramref name="thermal"/> true ise 100×150 mm (termal), değilse A4.
    /// Dönen dosya adı sipariş/etiket için öneri niteliğindedir.
    /// </summary>
    public async Task<(byte[] Bytes, string FileName)> DownloadLabelAsync(
        string slug,
        bool thermal = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Gönderi kimliği boş olamaz.", nameof(slug));
        }

        byte[] bytes = await _api.DownloadLabelAsync(slug, thermal ? "thermal" : "a4", cancellationToken)
            .ConfigureAwait(false);

        string safe = string.Join("_", slug.Split(System.IO.Path.GetInvalidFileNameChars()));
        string fileName = $"etiket_{safe}_{(thermal ? "termal" : "a4")}.pdf";

        return (bytes, fileName);
    }

    /// <summary>
    /// Gönderiyi iptal eder ve kullanıcıya gösterilecek cümleyi döndürür.
    /// İptal yalnız taslak / istek gönderildi / teslim bekleniyor durumlarında mümkündür.
    /// </summary>
    public async Task<string> CancelAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return "İptal edilecek gönderi seçilmedi.";
        }

        if (!IsConfigured)
        {
            return "Ship to More bağlantısı kurulmamış; iptal işlemi yapılamaz.";
        }

        try
        {
            await _api.CancelShipmentAsync(slug, cancellationToken).ConfigureAwait(false);
            return "Gönderi iptal edildi.";
        }
        catch (ShiptomoreApiException ex)
        {
            // En sık neden: gönderi çoktan yola çıkmış.
            return ex.StatusCode == 422
                ? "Bu gönderi iptal edilemedi. İptal yalnızca taslak, yeni oluşturulmuş veya teslim beklenen gönderilerde mümkündür; taşıyıcıya teslim edilmiş gönderi iptal edilemez."
                : "Gönderi iptal edilemedi: " + ex.Message;
        }
        catch (Exception ex)
        {
            return "Gönderi iptal edilemedi: " + ex.Message;
        }
    }
}
