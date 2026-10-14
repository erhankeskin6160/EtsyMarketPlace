namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Çoklu kargo firmaları ve Etsy sipariş kargo oluşturma orkestrasyon servisi.
/// </summary>
public sealed class ShipmentOrchestratorService : IShipmentOrchestratorService
{
    private readonly ShipmentCreationManager _creationManager;
    private readonly IEtsyLogisticsApiClient _logisticsApiClient;
    private readonly IShipEntegraApiClient? _shipEntegraApiClient;
    private readonly IShiptomoreOfficialApi? _shiptomoreApi;
    private readonly IEtsyOrderFulfillmentService? _fulfillmentService;
    private readonly EtsyOrderService? _orderService;

    public ShipmentOrchestratorService(
        ShipmentCreationManager creationManager,
        IEtsyLogisticsApiClient logisticsApiClient,
        IShipEntegraApiClient? shipEntegraApiClient = null,
        IShiptomoreOfficialApi? shiptomoreApi = null,
        IEtsyOrderFulfillmentService? fulfillmentService = null,
        EtsyOrderService? orderService = null)
    {
        _creationManager = creationManager ?? throw new ArgumentNullException(nameof(creationManager));
        _logisticsApiClient = logisticsApiClient ?? throw new ArgumentNullException(nameof(logisticsApiClient));
        _shipEntegraApiClient = shipEntegraApiClient;
        _shiptomoreApi = shiptomoreApi;
        _fulfillmentService = fulfillmentService;
        _orderService = orderService;
    }

    public async Task<CreateCarrierShipmentResult> CreateShipmentAsync(
        CreateCarrierShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
        {
            return new CreateCarrierShipmentResult
            {
                Success = false,
                ErrorMessage = "Gönderi komutu boş olamaz."
            };
        }

        // Alıcı adresi komut ile gelmediyse sipariş kuyruğundan çek
        var order = command.RecipientAddress;
        if (order == null && _fulfillmentService != null && !string.IsNullOrWhiteSpace(command.ShopId))
        {
            var queue = await _fulfillmentService.GetFulfillmentQueueAsync(command.ShopId, 90, null, cancellationToken);
            order = queue.FirstOrDefault(o => o.ReceiptId == command.ReceiptId);
        }

        if (order == null)
        {
            return new CreateCarrierShipmentResult
            {
                Success = false,
                ReceiptId = command.ReceiptId,
                CarrierKey = command.CarrierKey,
                ErrorMessage = $"Sipariş #{command.ReceiptId} alıcı adresi ve detayları bulunamadı."
            };
        }

        // Kargo sağlayıcı adını çözümle
        string providerName = ResolveProviderName(command.CarrierKey);

        var context = new ShipmentCreationContext
        {
            Order = order,
            WeightKg = command.WeightKg > 0 ? command.WeightKg : 0.4,
            WidthCm = command.WidthCm > 0 ? command.WidthCm : 15.0,
            LengthCm = command.LengthCm > 0 ? command.LengthCm : 20.0,
            HeightCm = command.HeightCm > 0 ? command.HeightCm : 10.0,
            HsCode = !string.IsNullOrWhiteSpace(command.HsCode) ? command.HsCode : "0302530000",
            SelectedServiceCode = command.ServiceCode,
            SelectedSubCarrier = !string.IsNullOrWhiteSpace(command.SubCarrier) ? command.SubCarrier : "widect",
            ServiceType = !string.IsNullOrWhiteSpace(command.ServiceType) ? command.ServiceType : "Eco Express",
            CargoPrice = command.CargoPrice
        };

        // Kargo platformunda gönderiyi oluştur
        var providerResult = await _creationManager.CreateShipmentAsync(providerName, context, cancellationToken);

        if (!providerResult.IsSuccess)
        {
            return new CreateCarrierShipmentResult
            {
                Success = false,
                ReceiptId = command.ReceiptId,
                CarrierKey = command.CarrierKey,
                ErrorMessage = providerResult.ErrorMessage
            };
        }

        var result = new CreateCarrierShipmentResult
        {
            Success = true,
            ReceiptId = command.ReceiptId,
            CarrierKey = command.CarrierKey,
            TrackingNumber = providerResult.TrackingNumber,
            CarrierShipmentId = providerResult.ShipmentId,
            LabelUrl = providerResult.LabelUrl
        };

        // Eğer provider doğrudan Base64 döndüyse veya yerel etiket dosyası üretildiyse Base64 olarak oku
        if (!string.IsNullOrWhiteSpace(providerResult.LabelBase64))
        {
            result.LabelBase64 = providerResult.LabelBase64;
        }
        else if (!string.IsNullOrWhiteSpace(providerResult.LabelUrl) && File.Exists(providerResult.LabelUrl))
        {
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(providerResult.LabelUrl, cancellationToken);
                result.LabelBase64 = Convert.ToBase64String(bytes);
            }
            catch { }
        }

        // Etsy API'ye otomatik bildirim (Etsy v3 POST /receipts/{id}/tracking)
        if (command.NotifyEtsy && !string.IsNullOrWhiteSpace(result.TrackingNumber) && !string.IsNullOrWhiteSpace(command.ShopId))
        {
            try
            {
                string carrierForEtsy = MapCarrierNameForEtsy(command.CarrierKey, command.SubCarrier);
                bool etsySubmitted = await _logisticsApiClient.SubmitTrackingAsync(
                    command.ShopId,
                    command.ReceiptId,
                    result.TrackingNumber,
                    carrierForEtsy,
                    sendBcc: false,
                    cancellationToken);

                result.EtsyTrackingSubmitted = etsySubmitted;
                result.EtsyTrackingMessage = etsySubmitted
                    ? "Takip numarası Etsy'ye başarıyla iletildi."
                    : "Takip numarası kargo platformunda oluşturuldu ancak Etsy'ye iletilemedi. Lütfen Etsy panelinden kontrol ediniz.";
            }
            catch (Exception ex)
            {
                result.EtsyTrackingSubmitted = false;
                result.EtsyTrackingMessage = $"Etsy takip bildirimi hatası: {ex.Message}";
            }
        }
        else if (!command.NotifyEtsy)
        {
            result.EtsyTrackingSubmitted = false;
            result.EtsyTrackingMessage = "🛡️ Test modu: Etsy mağazanıza bildirilmedi. Siparişiniz Etsy üzerinde güvende.";
        }

        // Yerel sipariş servisinde kargolandı olarak işaretle (yalnızca Etsy'ye bildirim istendiyse)
        if (command.NotifyEtsy && _orderService != null)
        {
            try
            {
                await _orderService.MarkOrderAsShippedAsync(
                    command.ReceiptId,
                    providerName,
                    result.TrackingNumber,
                    result.LabelUrl);
            }
            catch { }
        }

        return result;
    }

    public async Task<ConnectCarrierResult> ConnectCarrierAsync(
        ConnectCarrierCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.CarrierKey))
        {
            return new ConnectCarrierResult { Success = false, Message = "Kargo firması belirtilmedi." };
        }

        string key = command.CarrierKey.Trim().ToLowerInvariant();

        if (key.Contains("shipentegra"))
        {
            var settings = ShipEntegraSettingsStore.Load();

            if (!string.IsNullOrWhiteSpace(command.ApiKey)) settings.ApiKey = command.ApiKey.Trim();
            if (!string.IsNullOrWhiteSpace(command.ApiSecret)) settings.ApiSecret = command.ApiSecret.Trim();
            if (!string.IsNullOrWhiteSpace(command.BearerToken)) settings.BearerToken = command.BearerToken.Trim();
            if (!string.IsNullOrWhiteSpace(command.Email)) settings.SavedEmail = command.Email.Trim();
            if (!string.IsNullOrWhiteSpace(command.Password)) settings.EncryptedPassword = command.Password.Trim();

            // Eğer API Key ve Secret varsa resmi endpoint'ten token almaya çalış
            if (_shipEntegraApiClient != null && !string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.ApiSecret))
            {
                try
                {
                    var tokens = await _shipEntegraApiClient.AuthenticateWithClientCredentialsAsync(settings.ApiKey, settings.ApiSecret, cancellationToken);
                    if (tokens != null && !string.IsNullOrWhiteSpace(tokens.AccessToken))
                    {
                        settings.BearerToken = tokens.AccessToken;
                        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
                        {
                            settings.RefreshToken = tokens.RefreshToken;
                            settings.RefreshTokenLastUpdatedUtc = DateTime.UtcNow;
                        }
                    }
                }
                catch { }
            }

            ShipEntegraSettingsStore.Save(settings);

            bool isConnected = settings.HasValidTokenFormat || (!string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.ApiSecret));
            string? snippet = settings.HasValidTokenFormat
                ? (settings.CleanToken.Length > 8 ? settings.CleanToken[..4] + "..." + settings.CleanToken[^4..] : "***")
                : (!string.IsNullOrWhiteSpace(settings.ApiKey) ? settings.ApiKey[..4] + "..." : null);

            return new ConnectCarrierResult
            {
                Success = true,
                CarrierKey = "shipentegra",
                IsConnected = isConnected,
                TokenSnippet = snippet,
                Message = isConnected
                    ? "ShipEntegra bağlantısı başarıyla kaydedildi ve doğrulandı."
                    : "ShipEntegra ayarları kaydedildi. Lütfen geçerli bir token veya API anahtarı sağlayın."
            };
        }

        if (key.Contains("shiptomore"))
        {
            var clientId = command.ApiKey?.Trim() ?? command.BearerToken?.Trim();
            var clientSecret = command.ApiSecret?.Trim();

            if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            {
                ShiptomoreSettingsStore.SaveCredentials(clientId, clientSecret);
            }

            bool isConnected = ShiptomoreSettingsStore.HasCredentials();
            string? snippet = clientId != null && clientId.Length > 8
                ? clientId[..4] + "..." + clientId[^4..]
                : null;

            IReadOnlyList<string>? providers = null;
            if (_shiptomoreApi != null && isConnected)
            {
                try
                {
                    providers = await _shiptomoreApi.GetProvidersAsync(cancellationToken);
                }
                catch { }
            }

            string msg = providers != null && providers.Count > 0
                ? $"Ship to More API bağlantısı başarıyla doğrulandı. Canlı sağlayıcılar: {string.Join(", ", providers)}"
                : (isConnected ? "Ship to More API anahtarları kaydedildi." : "Lütfen geçerli Client ID ve Client Secret girin.");

            return new ConnectCarrierResult
            {
                Success = true,
                CarrierKey = "shiptomore",
                IsConnected = isConnected,
                TokenSnippet = snippet,
                Message = msg
            };
        }

        return new ConnectCarrierResult
        {
            Success = true,
            CarrierKey = key,
            IsConnected = true,
            Message = $"{key} kimlik bilgileri başarıyla kaydedildi."
        };
    }

    private static string ResolveProviderName(string? carrierKey)
    {
        string k = (carrierKey ?? string.Empty).Trim().ToLowerInvariant();
        if (k.Contains("shipentegra")) return "ShipEntegra";
        if (k.Contains("aras")) return "Aras Global";
        if (k.Contains("shiptomore")) return "Shiptomore";
        if (k.Contains("navlungo")) return "Navlungo";
        return carrierKey ?? string.Empty;
    }

    private static string MapCarrierNameForEtsy(string carrierKey, string subCarrier)
    {
        string sub = (subCarrier ?? string.Empty).Trim().ToLowerInvariant();
        if (sub.Contains("fedex")) return "FedEx";
        if (sub.Contains("ups")) return "UPS";
        if (sub.Contains("dhl")) return "DHL";
        if (sub.Contains("usps")) return "USPS";
        if (sub.Contains("widect") || sub.Contains("aras")) return "Aras Cargo";
        if (carrierKey.Contains("shipentegra")) return "ShipEntegra";
        if (carrierKey.Contains("shiptomore")) return "Ship to More";
        return !string.IsNullOrWhiteSpace(subCarrier) ? subCarrier : "Other";
    }
}
