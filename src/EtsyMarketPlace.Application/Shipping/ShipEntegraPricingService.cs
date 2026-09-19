namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// ShipEntegra fiyat sorgulama işlem sonucunu ve durum mesajını taşıyan model.
/// </summary>
public sealed class ShipEntegraPricingResult
{
    public bool Success { get; set; }
    public bool IsLive { get; set; }
    public bool TokenExpired { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public List<ShipEntegraQuoteOffer> Offers { get; set; } = [];
    public double CalculatedDesi { get; set; }
    public double BillableWeightKg { get; set; }
}

/// <summary>
/// ShipEntegra canlı API ve akıllı yedek fiyat hesaplama servisi.
/// </summary>
public sealed class ShipEntegraPricingService
{
    private readonly IShipEntegraApiClient? _apiClient;
    private readonly Func<ShipEntegraSettings> _settingsProvider;

    public ShipEntegraPricingService(
        IShipEntegraApiClient? apiClient = null,
        Func<ShipEntegraSettings>? settingsProvider = null)
    {
        _apiClient = apiClient;
        _settingsProvider = settingsProvider ?? (() => ShipEntegraSettingsStore.Load());
    }

    /// <summary>
    /// ShipEntegra kargo fiyat tekliflerini çeker. Token geçerliyse canlı API'yi kullanır;
    /// token dolmuşsa veya yoksa kullanıcıyı uyararak yedek listeyi sunar.
    /// </summary>
    public async Task<ShipEntegraPricingResult> GetQuotesAsync(
        ShipEntegraQuoteRequest request,
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
            return new ShipEntegraPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = true,
                StatusMessage = "⚠️ ShipEntegra oturum tokeni girilmemiş! Gösterilen fiyatlar yedek listedir. Canlı güncel teklifler için lütfen panelden tokeninizi yapıştırın.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }

        if (_apiClient == null)
        {
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ShipEntegraPricingResult
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
                return new ShipEntegraPricingResult
                {
                    Success = true,
                    IsLive = true,
                    TokenExpired = false,
                    StatusMessage = $"🟢 Canlı ShipEntegra API'sinden {liveOffers.Count} adet güncel teklif başarıyla alındı.",
                    Offers = liveOffers.OrderBy(o => o.TotalPrice).ToList(),
                    CalculatedDesi = desi,
                    BillableWeightKg = billable
                };
            }

            // Canlı teklif boş döndüyse fallback
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ShipEntegraPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = false,
                StatusMessage = "ℹ️ ShipEntegra bu rota için anlık teklif döndürmedi. Yedek sözleşme fiyatları listelendi.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }
        catch (ShipEntegraTokenExpiredException ex)
        {
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ShipEntegraPricingResult
            {
                Success = true,
                IsLive = false,
                TokenExpired = true,
                StatusMessage = $"⚠️ ShipEntegra oturum tokeninizin süresi doldu! ({ex.Message}) Gösterilen fiyatlar yedek listedir ve güncel olmayabilir. Lütfen panelden tokeninizi yenileyin.",
                Offers = fallbackOffers,
                CalculatedDesi = desi,
                BillableWeightKg = billable
            };
        }
        catch (Exception ex)
        {
            var fallbackOffers = GenerateFallbackOffers(billable, request.ReceiverCountry);
            return new ShipEntegraPricingResult
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
    /// Verilen kargo isteği için doğrudan yedek tarife tekliflerini döndürür.
    /// </summary>
    public Task<List<ShipEntegraQuoteOffer>> GetFallbackOffersAsync(ShipEntegraQuoteRequest request)
    {
        double billable = request.BillableWeightKg;
        var offers = GenerateFallbackOffers(billable, request.ReceiverCountry);
        return Task.FromResult(offers);
    }

    /// <summary>
    /// Çevrimdışı veya token süresi dolduğunda çalışan yedek tarife tablosu teklif üreticisi.
    /// ShipEntegra canlı veri modelini birebir yansıtır.
    /// </summary>
    public static List<ShipEntegraQuoteOffer> GenerateFallbackOffers(double billableWeightKg, string countryCode)
    {
        // 1.0 kg/desiye kadar baz kademe; üzerindeki her 0.5 kg için ek kademe
        double weightStep = Math.Max(1.0, Math.Ceiling(billableWeightKg * 2.0) / 2.0);
        double extraUnits = Math.Max(0, (weightStep - 1.0) / 0.5);

        decimal ekoPlusBase = 12.96m + ((decimal)extraUnits * 2.10m);
        decimal smartExpressBase = 19.01m + ((decimal)extraUnits * 3.20m);
        decimal widectBase = 19.55m + ((decimal)extraUnits * 3.10m);
        decimal expeditedBase = 20.68m + ((decimal)extraUnits * 3.40m);
        decimal expressBase = 22.36m + ((decimal)extraUnits * 3.60m);
        decimal upsExpressBase = 65.04m + ((decimal)extraUnits * 7.50m);

        bool isEu = IsEuropeanCountry(countryCode);
        if (isEu)
        {
            ekoPlusBase = Math.Round(ekoPlusBase * 0.90m, 2);
            smartExpressBase = Math.Round(smartExpressBase * 0.90m, 2);
        }

        return
        [
            new ShipEntegraQuoteOffer
            {
                ServiceName = "shipentegra-amerika-eko-plus",
                ClearServiceName = "ShipEntegra Amerika Eko Plus",
                ServiceType = "ECO",
                CargoPrice = Math.Round(ekoPlusBase * 0.85m, 2),
                FuelCost = Math.Round(ekoPlusBase * 0.15m, 2),
                TotalPrice = ekoPlusBase,
                Currency = "USD",
                AdditionalDescription = "Tahmini Teslim Süresi 3-6 iş günü<br>Amerika içi geri iade ücretsiz",
                IsBestCarrier = true,
                IsLivePrice = false
            },
            new ShipEntegraQuoteOffer
            {
                ServiceName = "shipentegra-smart-express",
                ClearServiceName = "ShipEntegra Smart Express",
                ServiceType = "EXPRESS",
                CargoPrice = Math.Round(smartExpressBase * 0.85m, 2),
                FuelCost = Math.Round(smartExpressBase * 0.15m, 2),
                TotalPrice = smartExpressBase,
                Currency = "USD",
                AdditionalDescription = "Tahmini Teslim Süresi 2-5 iş günü<br>100 $'a kadar sigortalı",
                IsBestCarrier = false,
                IsLivePrice = false
            },
            new ShipEntegraQuoteOffer
            {
                ServiceName = "shipentegra-widect",
                ClearServiceName = "ShipEntegra Widect",
                ServiceType = "ECO",
                CargoPrice = Math.Round(widectBase * 0.85m, 2),
                FuelCost = Math.Round(widectBase * 0.15m, 2),
                TotalPrice = widectBase,
                Currency = "USD",
                AdditionalDescription = "Tahmini Teslim Süresi 4-9 iş günü",
                IsBestCarrier = false,
                IsLivePrice = false
            },
            new ShipEntegraQuoteOffer
            {
                ServiceName = "shipentegra-expedited",
                ClearServiceName = "ShipEntegra Expedited",
                ServiceType = "EXPRESS",
                CargoPrice = Math.Round(expeditedBase * 0.78m, 2),
                FuelCost = Math.Round(expeditedBase * 0.22m, 2),
                TotalPrice = expeditedBase,
                Currency = "USD",
                AdditionalDescription = "Tahmini Teslim Süresi 3-5 iş günü",
                IsBestCarrier = false,
                IsLivePrice = false
            },
            new ShipEntegraQuoteOffer
            {
                ServiceName = "shipentegra-express",
                ClearServiceName = "ShipEntegra Express",
                ServiceType = "EXPRESS",
                CargoPrice = Math.Round(expressBase * 0.78m, 2),
                FuelCost = Math.Round(expressBase * 0.22m, 2),
                TotalPrice = expressBase,
                Currency = "USD",
                AdditionalDescription = "Tahmini Teslim Süresi 2-4 iş günü",
                IsBestCarrier = false,
                IsLivePrice = false
            },
            new ShipEntegraQuoteOffer
            {
                ServiceName = "shipentegra-ups-express",
                ClearServiceName = "ShipEntegra Ups Express",
                ServiceType = "EXPRESS",
                CargoPrice = Math.Round(upsExpressBase * 0.66m, 2),
                FuelCost = Math.Round(upsExpressBase * 0.34m, 2),
                TotalPrice = upsExpressBase,
                Currency = "USD",
                AdditionalDescription = "Tahmini Teslim Süresi 1-4 iş günü",
                IsBestCarrier = false,
                IsLivePrice = false
            }
        ];
    }

    private static bool IsEuropeanCountry(string code)
    {
        string c = (code ?? "").Trim().ToUpperInvariant();
        return c is "DE" or "FR" or "IT" or "ES" or "NL" or "BE" or "AT" or "PL" or "SE" or "DK" or "FI" or "GB" or "UK";
    }
}
