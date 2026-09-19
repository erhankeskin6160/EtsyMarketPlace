namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Fiyat sorgulama işlem sonucunu ve durum mesajını taşıyan model.
/// </summary>
public sealed class ArasGlobalPricingResult
{
    public bool Success { get; set; }
    public bool IsLive { get; set; }
    public bool TokenExpired { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public List<ArasGlobalQuoteOffer> Offers { get; set; } = [];
    public double CalculatedDesi { get; set; }
    public double BillableWeightKg { get; set; }
}

/// <summary>
/// Aras Global canlı API ve akıllı yedek fiyat hesaplama servisi.
/// </summary>
public sealed class ArasGlobalPricingService
{
    private readonly IArasGlobalApiClient? _apiClient;
    private readonly Func<ArasGlobalSettings> _settingsProvider;

    public ArasGlobalPricingService(
        IArasGlobalApiClient? apiClient = null,
        Func<ArasGlobalSettings>? settingsProvider = null)
    {
        _apiClient = apiClient;
        _settingsProvider = settingsProvider ?? (() => ArasGlobalSettingsStore.Load());
    }

    /// <summary>
    /// Kargo fiyat tekliflerini çeker. Token geçerliyse canlı API'yi kullanır;
    /// token dolmuşsa veya yoksa kullanıcıyı uyararak yedek listeyi sunar.
    /// </summary>
    public async Task<ArasGlobalPricingResult> GetQuotesAsync(
        ArasGlobalQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsProvider();
        double desi = request.CalculatedDesi;
        double billable = request.BillableWeightKg;

        // Token kontrolü
        string token = settings.CleanToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ArasGlobalPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = true,
                StatusMessage = "⚠️ Aras Global oturum tokeni girilmemiş! Gösterilen fiyatlar yedek tahmini listedir. Canlı güncel fiyatlar için lütfen panelden tokeninizi yapıştırın.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }

        if (_apiClient == null)
        {
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ArasGlobalPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = false,
                StatusMessage = "ℹ️ Canlı API istemcisi yapılandırılmamış. Yedek tahmini fiyatlar listelendi.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }

        try
        {
            var liveOffers = await _apiClient.FetchLiveQuotesAsync(request, token, cancellationToken);
            if (liveOffers.Count > 0)
            {
                return new ArasGlobalPricingResult
                {
                    Success = true,
                    IsLive = true,
                    TokenExpired = false,
                    StatusMessage = $"🟢 Canlı Aras Global API'sinden {liveOffers.Count} adet güncel teklif başarıyla alındı.",
                    Offers = liveOffers.OrderBy(o => o.Price).ToList(),
                    CalculatedDesi = desi,
                    BillableWeightKg = billable
                };
            }

            // Canlı teklif boş geldiyse fallback
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ArasGlobalPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = false,
                StatusMessage = "ℹ️ Aras Global bu rota için anlık teklif döndürmedi. Yedek sözleşme fiyatları listelendi.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }
        catch (ArasGlobalTokenExpiredException ex)
        {
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ArasGlobalPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = true,
                StatusMessage = $"⚠️ Aras Global oturum tokeninizin süresi doldu! ({ex.Message}) Gösterilen fiyatlar yedek listedir ve güncel olmayabilir. Lütfen panelden tokeninizi yenileyin.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }
        catch (Exception ex)
        {
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ArasGlobalPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = false,
                StatusMessage = $"⚠️ Canlı API bağlantı hatası: {ex.Message}. Yedek fiyat listesi gösteriliyor.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }
    }

    /// <summary>
    /// Çevrimdışı veya token süresi dolduğunda çalışan yedek tarife tablosu teklif üreticisi.
    /// </summary>
    public static List<ArasGlobalQuoteOffer> GenerateFallbackOffers(double billableWeightKg, string countryCode)
    {
        // ABD (US) için kullanıcının canlı test ettiği baz fiyatlar (0.4 kg: Widect $13.13, UPS $21.16)
        double weightStep = Math.Max(0.5, Math.Ceiling(billableWeightKg * 2.0) / 2.0); // 0.5 kg kademeleri
        double extraUnits = Math.Max(0, (weightStep - 0.5) / 0.5);

        decimal widectBase = 13.13m + ((decimal)extraUnits * 2.40m);
        decimal upsBase = 21.16m + ((decimal)extraUnits * 3.80m);

        bool isEu = IsEuropeanCountry(countryCode);
        if (isEu)
        {
            widectBase = Math.Round(widectBase * 0.85m, 2); // AB genelde bir miktar daha uygundur
            upsBase = Math.Round(upsBase * 0.90m, 2);
        }

        return
        [
            new ArasGlobalQuoteOffer
            {
                Cargo = "Widect",
                Price = widectBase,
                UnDiscountedPrice = widectBase,
                Currency = "USD",
                ProviderServiceType = "Eco Express",
                EstimatedStartDeliveryDate = 7,
                EstimatedEndDeliveryDate = 10,
                IsActive = true,
                IsLivePrice = false
            },
            new ArasGlobalQuoteOffer
            {
                Cargo = "UPS",
                Price = upsBase,
                UnDiscountedPrice = upsBase,
                Currency = "USD",
                ProviderServiceType = "Express",
                EstimatedStartDeliveryDate = 2,
                EstimatedEndDeliveryDate = 4,
                IsActive = true,
                IsLivePrice = false
            }
        ];
    }

    /// <summary>
    /// Verilen kargo isteği için doğrudan yedek tarife tekliflerini döndürür.
    /// </summary>
    public Task<List<ArasGlobalQuoteOffer>> GetFallbackOffersAsync(ArasGlobalQuoteRequest request)
    {
        double billable = request.BillableWeightKg;
        var offers = GenerateFallbackOffers(billable, request.ReceiverCountry);
        return Task.FromResult(offers);
    }

    private static bool IsEuropeanCountry(string code)
    {
        string c = (code ?? "").Trim().ToUpperInvariant();
        return c is "DE" or "FR" or "IT" or "ES" or "NL" or "BE" or "AT" or "PL" or "SE" or "DK" or "FI" or "GB" or "UK";
    }
}
