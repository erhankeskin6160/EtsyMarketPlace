namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Ship to More tekliflerini RESMÎ API'den alan kaynak.
///
/// Eski yol (çerezle web panelini kazıma) yerine <c>POST /v1/prices/calculate</c> kullanır:
/// böylece fiyatlar gerçek <c>provider_slug</c> / <c>service_slug</c> çiftleriyle gelir ve
/// gönderi oluşturma adımı bu slug'larla çalışabilir.
///
/// Anahtar girilmemişse <strong>boş liste</strong> döner — uydurma fiyat üretilmez.
/// </summary>
public sealed class ShiptomoreOfficialQuoteSource
{
    private readonly IShiptomoreOfficialApi _api;

    public ShiptomoreOfficialQuoteSource(IShiptomoreOfficialApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <summary>Kimlik bilgisi tanımlı mı? Değilse arayüz "teklif alınamadı" demeli.</summary>
    public bool IsAvailable => _api.HasCredentials;

    public async Task<List<ShiptomoreQuoteOffer>> GetQuotesAsync(
        ShiptomoreQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_api.HasCredentials)
        {
            return new List<ShiptomoreQuoteOffer>();
        }

        var priceRequest = BuildPriceRequest(request);

        var options = await _api.CalculatePricesAsync(priceRequest, cancellationToken).ConfigureAwait(false);

        return options
            .Where(o => !string.IsNullOrWhiteSpace(o.ProviderSlug) && !string.IsNullOrWhiteSpace(o.ServiceSlug))
            .Select(MapToOffer)
            .ToList();
    }

    /// <summary>Paket bağlamını resmî fiyat isteğine çevirir (saf, test edilebilir).</summary>
    public static ShiptomorePriceRequest BuildPriceRequest(ShiptomoreQuoteRequest request) => new()
    {
        CountryCode = (request.ToCountry ?? string.Empty).Trim().ToUpperInvariant(),
        PackageType = "custom",
        Parcels = new List<ShiptomoreParcelDimensions>
        {
            new()
            {
                Weight = request.WeightKg > 0 ? request.WeightKg : 0.4,
                Height = request.HeightCm > 0 ? request.HeightCm : 10,
                Width = request.WidthCm > 0 ? request.WidthCm : 15,
                Length = request.LengthCm > 0 ? request.LengthCm : 20,
                Qty = request.Quantity > 0 ? request.Quantity : 1
            }
        }
    };

    /// <summary>Fiyat seçeneğini kokpitin beklediği teklif biçimine çevirir.</summary>
    public static ShiptomoreQuoteOffer MapToOffer(ShiptomorePriceOption option) => new()
    {
        Carrier = option.ProviderSlug,
        ServiceName = option.ServiceSlug,
        DeliveryEstimate = string.Empty, // resmî fiyat yanıtı teslim süresi döndürmüyor
        Price = option.Price,
        Currency = string.IsNullOrWhiteSpace(option.Currency) ? "USD" : option.Currency,
        CurrencySymbol = "$",
        BillableWeight = option.BillingWeight,
        Note = "Ship to More API (canlı)",
        IsMemberRate = true
    };
}
