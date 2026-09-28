namespace EtsyMarketPlace.Application.Shipping;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Ship to More RESMÎ REST API'si (dev.shiptomore.com, openapi v1.1.0).
/// HTTP Basic ile kimlik doğrular: Client ID kullanıcı adı, Client Secret şifredir.
///
/// Mevcut <see cref="IShiptomoreApiClient"/>'ten ayrıdır: o çerezle web panelini kazıyan
/// eski yoldur, bu ise resmî sözleşmedir. Geçiş tamamlanınca eskisi kaldırılacak.
/// </summary>
public interface IShiptomoreOfficialApi
{
    /// <summary>Temel adres; yapılandırılabilir (dev/prod).</summary>
    string BaseUrl { get; set; }

    /// <summary>Kimlik bilgileri tanımlı mı (Client ID + Secret)?</summary>
    bool HasCredentials { get; }

    /// <summary>Bağlantı testi: sağlayıcı listesini çeker.</summary>
    Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default);

    /// <summary>Ham HS/GTIP kodu araması.</summary>
    Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default);

    /// <summary>Canlı fiyat teklifleri.</summary>
    Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default);

    /// <summary>Gönderi oluşturur (ve varsayılan olarak onaylar).</summary>
    Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default);

    /// <summary>Gönderi detayı.</summary>
    Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default);

    /// <summary>Etiketi PDF baytları olarak indirir ("a4" veya "thermal").</summary>
    Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default);

    /// <summary>Gönderiyi iptal eder (yalnız draft / requested / waiting-to-arrive).</summary>
    Task CancelShipmentAsync(string slug, CancellationToken ct = default);
}

/// <summary>
/// Ship to More API hatası. Gövdedeki doğrulama detayları okunabilir biçimde taşınır —
/// "bir şeyler ters gitti" yerine hangi alanın neden reddedildiği görülür.
/// </summary>
public sealed class ShiptomoreApiException : System.Exception
{
    public ShiptomoreApiException(string message, int statusCode, IReadOnlyList<string> details, string? rawBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        Details = details;
        RawBody = rawBody;
    }

    public int StatusCode { get; }
    public IReadOnlyList<string> Details { get; }
    public string? RawBody { get; }
}
