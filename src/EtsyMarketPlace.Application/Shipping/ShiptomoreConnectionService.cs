namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Bağlantı durumu — arayüzde tek satırda gösterilecek.</summary>
public sealed class ShiptomoreConnectionStatus
{
    public bool IsConnected { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CheckedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Ship to More bağlantısının kullanıcıya görünen yüzü.
///
/// ÖNEMLİ (şartnameden): ilk erişim anahtarı <strong>geliştirici portalından bir kez</strong>
/// alınır — <c>/auth/keys</c> uçları HTTP Basic (yani zaten var olan bir anahtar) istiyor,
/// dolayısıyla anahtarı programın kendi kendine üretmesi mümkün değil. Bu yüzden akış:
/// portalda bir kez anahtar oluştur → uygulamaya yapıştır → şifreli sakla → bir daha sorulmaz.
/// </summary>
public sealed class ShiptomoreConnectionService
{
    /// <summary>Kullanıcının anahtarı alacağı panel (arayüzde "Paneli Aç" olarak sunulur).</summary>
    public const string DeveloperPortalUrl = "https://dev.shiptomore.com";

    private readonly IShiptomoreOfficialApi _api;

    public ShiptomoreConnectionService(IShiptomoreOfficialApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <summary>Kayıtlı anahtar var mı (hiç ağa çıkmadan).</summary>
    public bool HasSavedCredentials() => ShiptomoreSettingsStore.HasCredentials();

    /// <summary>Kayıtlı anahtarları şifreli olarak yazar. Boşluklar kırpılır.</summary>
    public void SaveFromUserInput(string clientId, string clientSecret)
    {
        ShiptomoreSettingsStore.SaveCredentials(
            (clientId ?? string.Empty).Trim(),
            (clientSecret ?? string.Empty).Trim());
    }

    /// <summary>Kayıtlı anahtarları siler (bağlantıyı kesmek için).</summary>
    public void Forget()
    {
        var settings = ShiptomoreSettingsStore.Load();
        settings.ClientId = string.Empty;
        settings.ClientSecret = string.Empty;
        settings.EncryptedClientSecret = string.Empty;
        ShiptomoreSettingsStore.Save(settings);
    }

    /// <summary>Bağlantıyı gerçek bir çağrıyla doğrular ve kullanıcıya dönük cümle döndürür.</summary>
    public async Task<ShiptomoreConnectionStatus> TestAsync(CancellationToken ct = default)
    {
        if (!HasSavedCredentials())
        {
            return new ShiptomoreConnectionStatus
            {
                IsConnected = false,
                Message = "Ship to More erişim anahtarı henüz girilmemiş."
            };
        }

        try
        {
            var providers = await _api.GetProvidersAsync(ct).ConfigureAwait(false);
            return new ShiptomoreConnectionStatus
            {
                IsConnected = true,
                Message = providers.Count > 0
                    ? $"Bağlantı başarılı — {providers.Count} taşıyıcı sağlayıcı erişilebilir."
                    : "Bağlantı başarılı."
            };
        }
        catch (ShiptomoreApiException ex) when (ex.StatusCode is 401 or 403)
        {
            return new ShiptomoreConnectionStatus
            {
                IsConnected = false,
                Message = "Erişim anahtarı kabul edilmedi (yetkisiz). Anahtar iptal edilmiş olabilir; panelden yenisini oluşturup tekrar girin."
            };
        }
        catch (ShiptomoreApiException ex)
        {
            return new ShiptomoreConnectionStatus
            {
                IsConnected = false,
                Message = "Ship to More yanıtı: " + ex.Message
            };
        }
        catch (Exception ex)
        {
            return new ShiptomoreConnectionStatus
            {
                IsConnected = false,
                Message = "Ship to More'a ulaşılamadı: " + ex.Message
            };
        }
    }
}
