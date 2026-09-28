namespace EtsyMarketPlace.Infrastructure.Http;

using System;
using System.Net.Http;

/// <summary>
/// Uygulama genelinde <strong>tek</strong> HttpClient örneği.
///
/// Neden: her yerde <c>new HttpClient()</c> çağırmak, her örnek kendi bağlantı havuzunu
/// açtığı için soket tükenmesine (socket exhaustion) yol açar. Tek örnek, bağlantıları
/// yeniden kullanır. Özel işleyici (cookie, sahte tarayıcı başlığı) gereken yerlerde
/// yine kendi örnekleri oluşturulur; bu sınıf yalnızca "düz" istekler içindir.
/// </summary>
public static class SharedHttpClient
{
    private static readonly Lazy<HttpClient> LazyInstance = new(() => new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(100)
    });

    /// <summary>Paylaşılan örnek. Dispose edilmemelidir.</summary>
    public static HttpClient Instance => LazyInstance.Value;
}