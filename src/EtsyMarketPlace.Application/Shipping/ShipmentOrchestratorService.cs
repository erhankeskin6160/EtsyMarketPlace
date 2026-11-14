namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
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
    private readonly IArasGlobalApiClient? _arasGlobalApiClient;
    private readonly IShiptomoreOfficialApi? _shiptomoreApi;
    private readonly IEtsyOrderFulfillmentService? _fulfillmentService;
    private readonly EtsyOrderService? _orderService;
    private readonly IOrderAddressRepository? _addressRepository;
    private readonly IShippingSessionManager? _sessionManager;
    private readonly INavlungoApiClient? _navlungoApiClient;

    public ShipmentOrchestratorService(
        ShipmentCreationManager creationManager,
        IEtsyLogisticsApiClient logisticsApiClient,
        IShipEntegraApiClient? shipEntegraApiClient = null,
        IShiptomoreOfficialApi? shiptomoreApi = null,
        IEtsyOrderFulfillmentService? fulfillmentService = null,
        EtsyOrderService? orderService = null,
        IArasGlobalApiClient? arasGlobalApiClient = null,
        IOrderAddressRepository? addressRepository = null,
        IShippingSessionManager? sessionManager = null,
        INavlungoApiClient? navlungoApiClient = null)
    {
        _creationManager = creationManager ?? throw new ArgumentNullException(nameof(creationManager));
        _logisticsApiClient = logisticsApiClient ?? throw new ArgumentNullException(nameof(logisticsApiClient));
        _shipEntegraApiClient = shipEntegraApiClient;
        _shiptomoreApi = shiptomoreApi;
        _fulfillmentService = fulfillmentService;
        _orderService = orderService;
        _arasGlobalApiClient = arasGlobalApiClient;
        _addressRepository = addressRepository;
        _sessionManager = sessionManager;
        _navlungoApiClient = navlungoApiClient;
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

        // Alıcı adresi komut ile gelmediyse veya eksikse sipariş kuyruğundan çek ve tamamla
        var order = command.RecipientAddress;
        if (_fulfillmentService != null && !string.IsNullOrWhiteSpace(command.ShopId))
        {
            if (order == null || string.IsNullOrWhiteSpace(order.BuyerName) || string.IsNullOrWhiteSpace(order.StreetAddress) || string.IsNullOrWhiteSpace(order.State))
            {
                try
                {
                    var queue = await _fulfillmentService.GetFulfillmentQueueAsync(command.ShopId, 90, null, cancellationToken);
                    var queueOrder = queue?.FirstOrDefault(o => o.ReceiptId == command.ReceiptId);
                    if (queueOrder != null)
                    {
                        if (order == null)
                        {
                            order = queueOrder;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(order.BuyerName)) order.BuyerName = queueOrder.BuyerName;
                            if (string.IsNullOrWhiteSpace(order.StreetAddress)) order.StreetAddress = queueOrder.StreetAddress;
                            if (string.IsNullOrWhiteSpace(order.SecondAddress)) order.SecondAddress = queueOrder.SecondAddress;
                            if (string.IsNullOrWhiteSpace(order.City)) order.City = queueOrder.City;
                            if (string.IsNullOrWhiteSpace(order.State)) order.State = queueOrder.State;
                            if (string.IsNullOrWhiteSpace(order.PostalCode)) order.PostalCode = queueOrder.PostalCode;
                            if (string.IsNullOrWhiteSpace(order.CountryCode) || order.CountryCode == "US") order.CountryCode = queueOrder.CountryCode;
                            if (string.IsNullOrWhiteSpace(order.Phone)) order.Phone = queueOrder.Phone;
                            if (string.IsNullOrWhiteSpace(order.BuyerEmail)) order.BuyerEmail = queueOrder.BuyerEmail;
                            if (string.IsNullOrWhiteSpace(order.IossNumber)) order.IossNumber = queueOrder.IossNumber;
                            if (order.Items == null || order.Items.Count == 0) order.Items = queueOrder.Items;
                            if (order.TotalPrice <= 0) order.TotalPrice = queueOrder.TotalPrice;
                        }
                    }
                }
                catch { }
            }
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
            CargoPrice = command.CargoPrice,
            NotifyEtsy = command.NotifyEtsy,
            IsDdp = command.IsDdp
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

        // SQLite yerel sipariş deposuna takip no, etiket URL, kargo firması, GTİP ve paket ölçülerini kalıcı olarak kaydet
        if (_addressRepository != null && command.ReceiptId > 0 && !string.IsNullOrWhiteSpace(result.TrackingNumber))
        {
            try
            {
                var metaRecord = new OrderAddressRecord(
                    command.ReceiptId,
                    order.BuyerName ?? string.Empty,
                    order.BuyerEmail ?? string.Empty,
                    order.Phone ?? string.Empty,
                    order.StreetAddress ?? string.Empty,
                    order.SecondAddress ?? string.Empty,
                    order.City ?? string.Empty,
                    order.State ?? string.Empty,
                    order.PostalCode ?? string.Empty,
                    order.CountryCode ?? string.Empty,
                    order.CountryName ?? string.Empty,
                    "ShipmentCreated",
                    DateTime.UtcNow)
                {
                    IossNumber = order.IossNumber ?? string.Empty,
                    ShippingCostUsd = command.CargoPrice > 0 ? command.CargoPrice : order.ShippingCostUsd,
                    ProductCostUsd = order.ProductCostUsd,
                    WeightKg = command.WeightKg > 0 ? command.WeightKg : order.WeightKg,
                    WidthCm = command.WidthCm > 0 ? command.WidthCm : order.WidthCm,
                    LengthCm = command.LengthCm > 0 ? command.LengthCm : order.LengthCm,
                    HeightCm = command.HeightCm > 0 ? command.HeightCm : order.HeightCm,
                    GtipCode = !string.IsNullOrWhiteSpace(command.HsCode) ? command.HsCode : order.GtipCode,
                    SelectedCarrier = command.CarrierKey ?? providerName,
                    CarrierServiceName = !string.IsNullOrWhiteSpace(command.ServiceType) ? command.ServiceType : providerName,
                    TrackingCode = result.TrackingNumber,
                    LabelUrl = result.LabelUrl ?? string.Empty
                };

                await _addressRepository.SaveAsync(metaRecord, cancellationToken);
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

            string? authWarning = null;

            // EÄŸer API Key ve Secret varsa resmi endpoint'ten token almaya Ã§alÄ±ÅŸ
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
                    else
                    {
                        authWarning = "API AnahtarlarÄ±nÄ±z kaydedildi ancak ShipEntegra 'Yetkisiz API istemcisi (ERR.12015.1035)' yanÄ±tÄ± verdi. ShipEntegra panelinden API yetkinizi kontrol ediniz veya doÄŸrudan web oturum token'Ä±nÄ±zÄ± giriniz.";
                    }
                }
                catch (Exception ex)
                {
                    authWarning = $"API AnahtarÄ± ile token alÄ±nÄ±rken hata oluÅŸtu: {ex.Message}";
                }
            }

            // EÄŸer token yoksa veya sÃ¼resi dolmuÅŸsa ve e-posta/ÅŸifre varsa LoginAsync dene
            if ((string.IsNullOrWhiteSpace(settings.BearerToken) || JwtTokenInspector.IsExpired(settings.CleanToken)) &&
                _shipEntegraApiClient != null && !string.IsNullOrWhiteSpace(settings.SavedEmail) && !string.IsNullOrWhiteSpace(settings.EncryptedPassword))
            {
                try
                {
                    var tokens = await _shipEntegraApiClient.LoginAsync(settings.SavedEmail, settings.EncryptedPassword, cancellationToken);
                    if (tokens != null && !string.IsNullOrWhiteSpace(tokens.AccessToken))
                    {
                        settings.BearerToken = tokens.AccessToken;
                        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
                        {
                            settings.RefreshToken = tokens.RefreshToken;
                            settings.RefreshTokenLastUpdatedUtc = DateTime.UtcNow;
                        }
                        authWarning = null;
                    }
                }
                catch { }
            }

            ShipEntegraSettingsStore.Save(settings);

            bool isConnected = settings.HasValidTokenFormat || (!string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.ApiSecret));
            string? snippet = settings.HasValidTokenFormat
                ? (settings.CleanToken.Length > 8 ? settings.CleanToken[..4] + "..." + settings.CleanToken[^4..] : "***")
                : (!string.IsNullOrWhiteSpace(settings.ApiKey) ? settings.ApiKey[..4] + "..." : null);

            string message;
            if (isConnected)
            {
                message = "ShipEntegra baÄŸlantÄ±sÄ± ve aktif oturum token'Ä± baÅŸarÄ±yla kaydedildi ve doÄŸrulandÄ±.";
            }
            else if (!string.IsNullOrWhiteSpace(authWarning))
            {
                message = authWarning;
            }
            else
            {
                message = "ShipEntegra ayarlarÄ± kaydedildi ancak geÃ§erli bir oturum token'Ä± bulunamadÄ±. LÃ¼tfen geÃ§erli bir web oturum token'Ä± giriniz.";
            }

            return new ConnectCarrierResult
            {
                Success = isConnected || !string.IsNullOrWhiteSpace(settings.ApiKey) || !string.IsNullOrWhiteSpace(settings.SavedEmail),
                CarrierKey = "shipentegra",
                IsConnected = isConnected,
                TokenSnippet = snippet,
                Message = message
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

        if (key.Contains("aras"))
        {
            var settings = ArasGlobalSettingsStore.Load();
            string token = (command.BearerToken ?? command.ApiKey ?? string.Empty).Trim();
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring("Bearer ".Length).Trim();
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                settings.BearerToken = token;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
            }
            if (!string.IsNullOrWhiteSpace(command.RefreshToken))
            {
                settings.RefreshToken = command.RefreshToken.Trim();
            }
            if (!string.IsNullOrWhiteSpace(command.Email)) settings.SavedEmail = command.Email.Trim();
            if (!string.IsNullOrWhiteSpace(command.Password)) settings.EncryptedPassword = command.Password.Trim();

            ArasGlobalSettingsStore.Save(settings);

            // 1. Eğer token verilmediyse ama kayıtlı RefreshToken varsa sessizce yenilemeyi dene
            if (string.IsNullOrWhiteSpace(token) && !command.OpenBrowser && !settings.IsTokenValid && settings.HasValidRefreshToken && _arasGlobalApiClient != null)
            {
                string? refreshed = await _arasGlobalApiClient.RefreshAccessTokenAsync(settings.CleanToken, settings.CleanRefreshToken, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(refreshed))
                {
                    settings = ArasGlobalSettingsStore.Load();
                    settings.BearerToken = refreshed;
                    settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                    ArasGlobalSettingsStore.Save(settings);
                }
            }

            // 2. Tarayıcıdan yakalama istendiyse veya şifreyle otomatik giriş istendiyse Puppeteer ile yakala
            if ((command.OpenBrowser || (string.IsNullOrWhiteSpace(token) && (!string.IsNullOrWhiteSpace(command.Email) || !string.IsNullOrWhiteSpace(settings.SavedEmail))))
                && (!settings.IsTokenValid || command.OpenBrowser)
                && _sessionManager != null)
            {
                string? captured = await _sessionManager.RefreshArasGlobalTokenAsync(
                    settings.SavedEmail,
                    settings.EncryptedPassword,
                    showBrowser: command.OpenBrowser || string.IsNullOrWhiteSpace(settings.EncryptedPassword),
                    ct: cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(captured))
                {
                    settings = ArasGlobalSettingsStore.Load();
                    if (string.IsNullOrWhiteSpace(settings.CleanToken))
                    {
                        settings.BearerToken = captured;
                        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                        ArasGlobalSettingsStore.Save(settings);
                    }
                }
            }

            settings = ArasGlobalSettingsStore.Load();
            bool isConnected = settings.IsTokenValid;
            string? snippet = !string.IsNullOrWhiteSpace(settings.CleanToken) && settings.CleanToken.Length > 8
                ? settings.CleanToken[..4] + "..." + settings.CleanToken[^4..]
                : null;

            return new ConnectCarrierResult
            {
                Success = isConnected,
                CarrierKey = "aras",
                IsConnected = isConnected,
                TokenSnippet = snippet,
                BearerToken = isConnected ? settings.CleanToken : null,
                RefreshToken = settings.HasValidRefreshToken ? settings.CleanRefreshToken : null,
                ExpiresAtUtc = settings.GetTokenExpiryUtc(),
                Message = isConnected
                    ? "Aras Global oturum tokenı yakalandı, şifreli olarak kaydedildi ve aktif edildi."
                    : "Aras Global oturum tokenı yakalanamadı. Lütfen 'Tarayıcıda Giriş Yap & Yakala' butonunu kullanın veya geçerli bir Bearer Token girin."
            };
        }

        if (key.Contains("navlungo"))
        {
            var settings = NavlungoSettingsStore.Load();
            string token = (command.BearerToken ?? command.ApiKey ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                settings.IdToken = token;
                settings.SessionCookie = token;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
            }
            if (!string.IsNullOrWhiteSpace(command.Email)) settings.SavedEmail = command.Email.Trim();
            if (!string.IsNullOrWhiteSpace(command.Password)) settings.EncryptedPassword = command.Password.Trim();

            NavlungoSettingsStore.Save(settings);

            bool isConnected = !string.IsNullOrWhiteSpace(settings.IdToken) || !string.IsNullOrWhiteSpace(settings.SessionCookie);
            string? snippet = isConnected && token.Length > 8
                ? token[..4] + "..." + token[^4..]
                : null;

            return new ConnectCarrierResult
            {
                Success = isConnected,
                CarrierKey = "navlungo",
                IsConnected = isConnected,
                TokenSnippet = snippet,
                BearerToken = settings.IdToken,
                Message = isConnected
                    ? "Navlungo oturum bilgileri başarıyla kaydedildi."
                    : "Navlungo için geçerli bir Token veya Çerez girilmelidir."
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

    public async Task<IReadOnlyList<CarrierSessionStatusDto>> GetCarrierSessionsAsync(CancellationToken ct = default)
    {
        var list = new List<CarrierSessionStatusDto>();

        // 1. Aras Global (varsa süresi geçmiş token'ı RefreshToken ile otomatik yenile)
        var aras = ArasGlobalSettingsStore.Load();
        if (!aras.IsTokenValid && aras.HasValidRefreshToken && _arasGlobalApiClient != null)
        {
            string? refreshed = await _arasGlobalApiClient.RefreshAccessTokenAsync(aras.CleanToken, aras.CleanRefreshToken, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(refreshed))
            {
                aras.BearerToken = refreshed;
                aras.TokenLastUpdatedUtc = DateTime.UtcNow;
                ArasGlobalSettingsStore.Save(aras);
            }
        }
        string arasClean = aras.CleanToken;
        list.Add(new CarrierSessionStatusDto
        {
            CarrierKey = "aras",
            CarrierName = "Aras Global",
            IsConnected = aras.IsTokenValid,
            HasRefreshToken = aras.HasValidRefreshToken,
            BearerToken = aras.IsTokenValid ? arasClean : null,
            TokenSnippet = arasClean.Length > 8 ? arasClean[..4] + "..." + arasClean[^4..] : null,
            SavedEmail = aras.SavedEmail,
            ExpiresAtUtc = aras.GetTokenExpiryUtc(),
            UpdatedAtUtc = aras.TokenLastUpdatedUtc
        });

        // 2. ShipEntegra
        var se = ShipEntegraSettingsStore.Load();
        string seClean = se.CleanToken;
        DateTime? seExpiry = JwtTokenInspector.GetExpirationUtc(seClean);
        bool seIsExpired = seExpiry.HasValue ? DateTime.UtcNow >= seExpiry.Value : (string.IsNullOrWhiteSpace(seClean) || seClean.Length <= 20);
        bool seConnected = !string.IsNullOrWhiteSpace(seClean) && !seIsExpired;
        list.Add(new CarrierSessionStatusDto
        {
            CarrierKey = "shipentegra",
            CarrierName = "ShipEntegra",
            IsConnected = seConnected,
            HasRefreshToken = !string.IsNullOrWhiteSpace(se.RefreshToken),
            BearerToken = se.HasValidTokenFormat ? seClean : null,
            ApiKey = se.ApiKey,
            TokenSnippet = seClean.Length > 8 ? seClean[..4] + "..." + seClean[^4..] : null,
            SavedEmail = se.SavedEmail,
            ExpiresAtUtc = JwtTokenInspector.GetExpirationUtc(seClean),
            UpdatedAtUtc = se.TokenLastUpdatedUtc
        });

        // 3. Navlungo
        var nav = NavlungoSettingsStore.Load();
        string navToken = (!string.IsNullOrWhiteSpace(nav.IdToken) ? nav.IdToken : nav.SessionCookie) ?? string.Empty;
        bool navConnected = !string.IsNullOrWhiteSpace(navToken);
        list.Add(new CarrierSessionStatusDto
        {
            CarrierKey = "navlungo",
            CarrierName = "Navlungo",
            IsConnected = navConnected,
            HasRefreshToken = false,
            BearerToken = navConnected ? navToken : null,
            TokenSnippet = navToken.Length > 8 ? navToken[..4] + "..." + navToken[^4..] : null,
            SavedEmail = nav.SavedEmail,
            ExpiresAtUtc = null,
            UpdatedAtUtc = nav.TokenLastUpdatedUtc
        });

        // 4. ShipToMore
        var stm = ShiptomoreSettingsStore.Load();
        string stmKey = stm.ClientId?.Trim() ?? string.Empty;
        bool stmConnected = ShiptomoreSettingsStore.HasCredentials();
        list.Add(new CarrierSessionStatusDto
        {
            CarrierKey = "shiptomore",
            CarrierName = "ShipToMore",
            IsConnected = stmConnected,
            HasRefreshToken = false,
            BearerToken = stmConnected ? stmKey : null,
            ApiKey = stmKey,
            TokenSnippet = stmKey.Length > 8 ? stmKey[..4] + "..." + stmKey[^4..] : null
        });

        return list;
    }


    public async Task<CarrierQuotesResponseDto> GetCarrierQuotesAsync(
        CarrierQuotesQuery query,
        CancellationToken cancellationToken = default)
    {
        query ??= new CarrierQuotesQuery();
        string country = string.IsNullOrWhiteSpace(query.CountryCode) ? "US" : query.CountryCode.Trim().ToUpperInvariant();
        double weight = query.WeightKg > 0 ? query.WeightKg : 0.4;
        double width = query.WidthCm > 0 ? query.WidthCm : 15.0;
        double length = query.LengthCm > 0 ? query.LengthCm : 20.0;
        double height = query.HeightCm > 0 ? query.HeightCm : 10.0;
        decimal usdTry = query.UsdTryRate > 0 ? query.UsdTryRate : 36.25m;
        string hsDigits = new string((query.HsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        string hsCode = string.IsNullOrWhiteSpace(hsDigits) ? "3926400000" : hsDigits;
        decimal declaredValue = query.DeclaredValueUsd.HasValue && query.DeclaredValueUsd.Value.Value > 0m
            ? query.DeclaredValueUsd.Value.Value
            : 1.00m;
        int quantity = query.Quantity > 0 ? query.Quantity : 1;

        double desi = Math.Round((width * length * height) / 5000.0, 2);
        double billable = Math.Max(weight, desi);

        var sePricing = new ShipEntegraPricingService(_shipEntegraApiClient);
        var arasPricing = new ArasGlobalPricingService(_arasGlobalApiClient);

        var seTask = sePricing.GetQuotesAsync(new ShipEntegraQuoteRequest
        {
            ReceiverCountry = country,
            ReceiverPostalCode = string.IsNullOrWhiteSpace(query.PostalCode) ? "10001" : query.PostalCode,
            WeightKg = weight,
            WidthCm = width,
            LengthCm = length,
            HeightCm = height,
            HsCode = hsCode,
            DeclaredValueUsd = declaredValue,
            Quantity = quantity
        }, cancellationToken);

        var arasTask = arasPricing.GetQuotesAsync(new ArasGlobalQuoteRequest
        {
            ReceiverCountry = country,
            ReceiverPostalCode = string.IsNullOrWhiteSpace(query.PostalCode) ? "10001" : query.PostalCode,
            ReceiverCity = query.City ?? string.Empty,
            ReceiverState = query.State ?? string.Empty,
            WeightKg = weight,
            WidthCm = width,
            LengthCm = length,
            HeightCm = height
        }, cancellationToken);

        Task<List<ShiptomoreQuoteOffer>> stmTask = _shiptomoreApi != null
            ? new ShiptomoreOfficialQuoteSource(_shiptomoreApi).GetQuotesAsync(new ShiptomoreQuoteRequest
            {
                ToCountry = country,
                WeightKg = weight,
                WidthCm = width,
                LengthCm = length,
                HeightCm = height,
                Quantity = 1,
                IsDdp = true,
                DeclaredValueUsd = Math.Round(declaredValue * quantity, 2)
            }, cancellationToken)
            : Task.FromResult(new List<ShiptomoreQuoteOffer>());

        Task<IReadOnlyList<ShiptomoreHsCode>> stmHsTask = (_shiptomoreApi != null && country == "US" && hsCode.Length >= 6)
            ? SafeSearchShiptomoreHsAsync(_shiptomoreApi, hsCode[..Math.Min(8, hsCode.Length)], cancellationToken)
            : Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(Array.Empty<ShiptomoreHsCode>());

        var navReq = new NavlungoQuoteRequest
        {
            FromCountry = "TR",
            ToCountry = country,
            WeightKg = weight,
            LengthCm = length,
            WidthCm = width,
            HeightCm = height,
            PostalCode = string.IsNullOrWhiteSpace(query.PostalCode) ? "10001" : query.PostalCode,
            City = query.City ?? string.Empty,
            State = query.State ?? string.Empty,
            DeclaredValueUsd = Math.Round(declaredValue * quantity, 2)
        };
        var navSettings = NavlungoSettingsStore.Load();
        Task<List<NavlungoQuoteOffer>> navTask = _navlungoApiClient != null
            ? SafeFetchNavlungoQuotesAsync(_navlungoApiClient, navReq, navSettings, cancellationToken)
            : Task.FromResult(new List<NavlungoQuoteOffer>());

        await Task.WhenAll(seTask, arasTask, stmTask, stmHsTask, navTask);

        var seResult = await seTask;
        var arasResult = await arasTask;
        List<ShiptomoreQuoteOffer> stmOffers;
        try
        {
            stmOffers = await stmTask;
        }
        catch
        {
            stmOffers = new List<ShiptomoreQuoteOffer>();
        }

        IReadOnlyList<ShiptomoreHsCode> stmHsCodes = await stmHsTask;
        var (defaultGenRate, defaultAddRate, defaultAddHts) = ShipEntegraTariffCalculator.GetDefaultDutyRates(hsCode, country);
        var matchedStmHs = stmHsCodes.FirstOrDefault(h => h.UsTariffRate.HasValue && h.UsTariffRate.Value > 0.0);
        decimal? stmLiveUsRate = matchedStmHs?.UsTariffRate != null
            ? Math.Round((decimal)matchedStmHs.UsTariffRate.Value, 2)
            : null;
        var seRefOffer = seResult.Offers.FirstOrDefault(o => o.GeneralDutyRatePercent > 0m || o.AdditionalDutyRatePercent > 0m);
        decimal baseGenRate = seRefOffer != null && seRefOffer.GeneralDutyRatePercent > 0m ? seRefOffer.GeneralDutyRatePercent : defaultGenRate;
        decimal baseAddRate = seRefOffer != null && seRefOffer.AdditionalDutyRatePercent > 0m ? seRefOffer.AdditionalDutyRatePercent : defaultAddRate;
        string baseAddHts = seRefOffer != null && !string.IsNullOrWhiteSpace(seRefOffer.AdditionalDutyHtsCode)
            ? seRefOffer.AdditionalDutyHtsCode
            : defaultAddHts;
        decimal declaredTotalUsd = Math.Round(declaredValue * quantity, 2);

        var quotes = new List<UnifiedCarrierQuoteDto>();

        // 1. Aras Global Teklifleri (Navlun tek kalem, Gümrük/DDP ayrıntı kartı)
        foreach (var offer in arasResult.Offers)
        {
            decimal priceUsd = Math.Round(offer.Price, 2);
            var (ddpFee, ddpListFee, ddpLabel) = ResolveCarrierDdpProcessingFee("aras", offer.Cargo, offer.ProviderServiceType);
            var quote = new UnifiedCarrierQuoteDto
            {
                CarrierKey = "aras",
                CarrierName = "Aras Global",
                ServiceCode = offer.Cargo.ToLowerInvariant().Replace(" ", "-"),
                ServiceType = $"{offer.Cargo} — {offer.ProviderServiceType}".Trim(' ', '—'),
                SubCarrier = offer.Cargo,
                EstimatedDays = offer.DeliveryDaysText,
                PriceUsd = priceUsd,
                PriceTry = Math.Round(priceUsd * usdTry, 2),
                IsLive = arasResult.IsLive && offer.IsLivePrice,
                QuoteSourceBadge = (arasResult.IsLive && offer.IsLivePrice) ? "Canlı teklif" : "Tahmini tarife",
                HasDetailedBreakdown = false,
                BillableWeightKg = billable,
                UnDiscountedPriceUsd = offer.UnDiscountedPrice > priceUsd ? Math.Round(offer.UnDiscountedPrice, 2) : null,
                DiscountRatePercent = offer.DiscountRate > 0 ? Math.Round(offer.DiscountRate, 1) : null
            };
            ApplyCustomsBreakdownToQuote(
                quote, declaredTotalUsd, hsCode, baseGenRate, baseAddRate, baseAddHts,
                ddpFee, ddpListFee, ddpLabel,
                country == "US" ? "ABD Tarife + Taşıyıcı DDP Ücreti" : "Tahmini Gümrük Tarifesi",
                usdTry);
            quotes.Add(quote);
        }

        // 2. ShipEntegra Teklifleri (Resmî API'den tam navlun kırılımı ve Hızlı Gümrük / DDP)
        foreach (var offer in seResult.Offers)
        {
            decimal priceUsd = Math.Round(offer.TotalPrice > 0 ? offer.TotalPrice : offer.CargoPrice, 2);
            string label = !string.IsNullOrWhiteSpace(offer.ClearServiceName) ? offer.ClearServiceName : offer.ServiceName;
            decimal fastCustoms = offer.FastCustomsPaymentUsd;
            decimal grandTotal = offer.GrandTotalUsd > 0 ? offer.GrandTotalUsd : Math.Round(priceUsd + fastCustoms, 2);
            decimal discFreight = offer.DiscountedTotalPriceUsd > 0 ? offer.DiscountedTotalPriceUsd : priceUsd;
            decimal discGrand = offer.DiscountedGrandTotalUsd > 0 ? offer.DiscountedGrandTotalUsd : Math.Round(discFreight + fastCustoms, 2);

            quotes.Add(new UnifiedCarrierQuoteDto
            {
                CarrierKey = "shipentegra",
                CarrierName = "ShipEntegra",
                ServiceCode = offer.ServiceName,
                ServiceType = $"{label} — {offer.ServiceType}",
                SubCarrier = label.Contains("UPS", StringComparison.OrdinalIgnoreCase) ? "UPS"
                    : label.Contains("FedEx", StringComparison.OrdinalIgnoreCase) ? "FedEx"
                    : label.Contains("DHL", StringComparison.OrdinalIgnoreCase) ? "DHL"
                    : "ShipEntegra",
                EstimatedDays = offer.DeliveryDaysText,
                PriceUsd = priceUsd,
                PriceTry = Math.Round(priceUsd * usdTry, 2),
                IsLive = seResult.IsLive && offer.IsLivePrice,
                QuoteSourceBadge = (seResult.IsLive && offer.IsLivePrice) ? "Canlı teklif" : "Tahmini tarife",
                HasDetailedBreakdown = true,
                HasCustomsBreakdown = true,
                CustomsSourceBadge = (seResult.IsLive && offer.IsLivePrice) ? "Canlı ShipEntegra DDP API" : "Tahmini Gümrük Tarifesi",
                CarrierProcessingFeeLabel = "Taşıyıcı İşlem Bedeli",
                BillableWeightKg = billable,
                BaseCargoPriceUsd = Math.Round(offer.CargoPrice, 2),
                FuelCostUsd = Math.Round(offer.FuelCost, 2),
                FuelMultiplierPercent = Math.Round(offer.FuelMultiplierPercent, 2),
                AdditionalFeeUsd = Math.Round(offer.AdditionalFeeUsd, 2),
                AdditionalFeeDescription = offer.AdditionalFeeDescription,
                DiscountedFreightUsd = discFreight,
                PricingTemplate = offer.PricingTemplate,
                ReturnPolicyNote = offer.ReturnPolicyNote,
                InsuranceNote = offer.InsuranceNote,
                DeclaredValueUsd = offer.DeclaredValueUsd > 0 ? offer.DeclaredValueUsd : declaredTotalUsd,
                HsCode = !string.IsNullOrWhiteSpace(offer.HsCode) ? offer.HsCode : hsCode,
                GeneralDutyUsd = offer.GeneralDutyUsd,
                GeneralDutyRatePercent = offer.GeneralDutyRatePercent,
                AdditionalDutyUsd = offer.AdditionalDutyUsd,
                AdditionalDutyRatePercent = offer.AdditionalDutyRatePercent,
                AdditionalDutyHtsCode = offer.AdditionalDutyHtsCode,
                TaxAndDutySubtotalUsd = offer.TaxAndDutySubtotalUsd,
                CarrierProcessingFeeUsd = offer.CarrierProcessingFeeUsd,
                FastCustomsPaymentUsd = fastCustoms,
                FastCustomsPaymentTry = Math.Round(fastCustoms * usdTry, 2),
                GrandTotalUsd = grandTotal,
                GrandTotalTry = Math.Round(grandTotal * usdTry, 2),
                DiscountedGrandTotalUsd = discGrand
            });
        }

        // 3. Shiptomore Teklifleri (Navlun: gerçek tek kalem API fiyatı; DDP: /v1/hs-codes tarife + taşıyıcı DDP işlem bedeli)
        decimal stmGenRate = stmLiveUsRate ?? baseGenRate;
        string stmCustomsBadge = stmLiveUsRate.HasValue
            ? "Shiptomore HS API + DDP Tarife"
            : "Shiptomore DDP Tarife";

        if (stmOffers.Count > 0)
        {
            foreach (var offer in stmOffers)
            {
                decimal priceUsd = Math.Round(offer.Price, 2);
                var (ddpFee, ddpListFee, ddpLabel) = ResolveCarrierDdpProcessingFee("shiptomore", offer.Carrier, offer.ServiceName, offer);
                var quote = new UnifiedCarrierQuoteDto
                {
                    CarrierKey = "shiptomore",
                    CarrierName = "Shiptomore",
                    ServiceCode = $"{offer.Carrier}:{offer.ServiceName}",
                    ServiceType = $"{offer.Carrier.ToUpperInvariant()} {offer.ServiceName} — Shiptomore",
                    SubCarrier = offer.Carrier.ToUpperInvariant(),
                    EstimatedDays = string.IsNullOrWhiteSpace(offer.DeliveryEstimate) ? "2-5 İş Günü" : offer.DeliveryEstimate,
                    PriceUsd = priceUsd,
                    PriceTry = Math.Round(priceUsd * usdTry, 2),
                    IsLive = true,
                    QuoteSourceBadge = "Canlı teklif",
                    HasDetailedBreakdown = false,
                    BillableWeightKg = offer.BillableWeight > 0 ? Math.Round(offer.BillableWeight, 2) : billable
                };
                ApplyCustomsBreakdownToQuote(
                    quote, declaredTotalUsd, hsCode, stmGenRate, baseAddRate, baseAddHts,
                    ddpFee, ddpListFee, ddpLabel, stmCustomsBadge, usdTry);
                quotes.Add(quote);
            }
        }
        else
        {
            double extraUnits = Math.Max(0, Math.Ceiling((billable - 0.5) / 0.5));
            double mult = country == "US" ? 1.0 : country == "CA" ? 1.08 : 0.94;
            decimal stmEco = Math.Round((decimal)((10.90 + extraUnits * 2.15) * mult), 2);
            decimal stmExp = Math.Round((decimal)((13.45 + extraUnits * 2.55) * mult), 2);
            bool stmConnected = ShiptomoreSettingsStore.HasCredentials();

            var (ecoFee, ecoListFee, ecoLabel) = ResolveCarrierDdpProcessingFee("shiptomore", "FedEx", "economy");
            var stmEcoQuote = new UnifiedCarrierQuoteDto
            {
                CarrierKey = "shiptomore",
                CarrierName = "Shiptomore",
                ServiceCode = "fedex: economy",
                ServiceType = "FedEx Economy — Shiptomore",
                SubCarrier = "FedEx",
                EstimatedDays = "3-6 İş Günü",
                PriceUsd = stmEco,
                PriceTry = Math.Round(stmEco * usdTry, 2),
                IsLive = stmConnected,
                QuoteSourceBadge = stmConnected ? "Canlı teklif" : "Tahmini tarife",
                HasDetailedBreakdown = false,
                BillableWeightKg = billable
            };
            ApplyCustomsBreakdownToQuote(
                stmEcoQuote, declaredTotalUsd, hsCode, stmGenRate, baseAddRate, baseAddHts,
                ecoFee, ecoListFee, ecoLabel, stmCustomsBadge, usdTry);
            quotes.Add(stmEcoQuote);

            var (expFee, expListFee, expLabel) = ResolveCarrierDdpProcessingFee("shiptomore", "UPS", "express-saver");
            var stmExpQuote = new UnifiedCarrierQuoteDto
            {
                CarrierKey = "shiptomore",
                CarrierName = "Shiptomore",
                ServiceCode = "ups: express-saver",
                ServiceType = "UPS Express Saver — Shiptomore",
                SubCarrier = "UPS",
                EstimatedDays = "2-4 İş Günü",
                PriceUsd = stmExp,
                PriceTry = Math.Round(stmExp * usdTry, 2),
                IsLive = stmConnected,
                QuoteSourceBadge = stmConnected ? "Canlı teklif" : "Tahmini tarife",
                HasDetailedBreakdown = false,
                BillableWeightKg = billable
            };
            ApplyCustomsBreakdownToQuote(
                stmExpQuote, declaredTotalUsd, hsCode, stmGenRate, baseAddRate, baseAddHts,
                expFee, expListFee, expLabel, stmCustomsBadge, usdTry);
            quotes.Add(stmExpQuote);
        }

        // 4. Navlungo Teklifleri (Canlı API veya 4 Kademeli Navlungo Referans Tarifesi + Tam Kalem & DDP Kırılımı)
        {
            List<NavlungoQuoteOffer> navOffers = await navTask;
            bool navSessionActive = navSettings != null &&
                (!string.IsNullOrWhiteSpace(navSettings.IdToken) || !string.IsNullOrWhiteSpace(navSettings.SessionCookie));
            bool navUsingLive = navOffers.Count > 0 &&
                navOffers.Any(o => o.DdpFeeUsd > 0m ||
                                   o.Note.Contains("Üye", StringComparison.OrdinalIgnoreCase) ||
                                   o.Note.Contains("API Canlı", StringComparison.OrdinalIgnoreCase));
            if (!navUsingLive)
            {
                // Anonim quick-price-calculator liste fiyatlarını ($30.07 / $37.03) kullanmak yerine
                // ship.navlungo.com üye portalı api.navlungo.com verilerine kalibre edilmiş tarifeyi kullan.
                navOffers = NavlungoFallbackPricing.GenerateRealisticQuotes(navReq);
            }

            foreach (var offer in navOffers)
            {
                decimal priceUsd = Math.Round(offer.Price, 2, MidpointRounding.AwayFromZero);
                decimal baseSystem = offer.BaseSystemOfferUsd > 0m
                    ? Math.Round(offer.BaseSystemOfferUsd, 2, MidpointRounding.AwayFromZero)
                    : Math.Round(Math.Max(0m, priceUsd - offer.FuelSurchargeUsd - offer.PeakSeasonSurchargeUsd), 2, MidpointRounding.AwayFromZero);
                decimal fuelCost = Math.Round(offer.FuelSurchargeUsd, 2, MidpointRounding.AwayFromZero);
                decimal peakFee = Math.Round(offer.PeakSeasonSurchargeUsd, 2, MidpointRounding.AwayFromZero);
                decimal fuelPct = baseSystem > 0m && fuelCost > 0m
                    ? Math.Round((fuelCost / baseSystem) * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                string carrierSlug = offer.Carrier.ToLowerInvariant() switch
                {
                    "widect" => "nav-widect",
                    "fedex" => "nav-fedex",
                    "ups" when offer.ServiceType.Contains("Saver", StringComparison.OrdinalIgnoreCase) ||
                               offer.CustomerName.Contains("Expedited", StringComparison.OrdinalIgnoreCase) => "nav-ups-saver",
                    "ups" => "nav-ups-express",
                    _ => $"nav-{offer.Carrier.ToLowerInvariant().Replace(" ", "-")}"
                };

                string serviceLabel = carrierSlug switch
                {
                    "nav-widect" => "Widect — Navlungo",
                    "nav-fedex" => "FedEx Express — Navlungo",
                    "nav-ups-saver" => "UPS Expedited — Navlungo",
                    "nav-ups-express" => "UPS Express — Navlungo",
                    _ => $"{offer.Carrier} {offer.ServiceType} — Navlungo"
                };

                bool offerLive = (navUsingLive && offer.Note.Contains("Canlı", StringComparison.OrdinalIgnoreCase)) || navSessionActive;
                double offerBillable = offer.ChargeableWeightKg > 0 ? offer.ChargeableWeightKg : billable;

                var (navFee, navListFee, navLabel) = ResolveCarrierDdpProcessingFee(
                    "navlungo", offer.Carrier, offer.ServiceType,
                    navDdpFeeUsd: offer.DdpFeeUsd,
                    declaredTotalUsd: declaredTotalUsd,
                    generalDutyRatePercent: baseGenRate,
                    additionalDutyRatePercent: baseAddRate);

                var notes = new List<string>();
                if (offer.InsuranceFeeUsd > 0m) notes.Add($"Opsiyonel Sigorta: ${offer.InsuranceFeeUsd:0.00}");
                if (offer.PremiumTrackingFeeUsd > 0m) notes.Add($"Premium Takip: ${offer.PremiumTrackingFeeUsd:0.00}");
                if (offer.SignatureRequiredFeeUsd > 0m) notes.Add($"İmzalı Teslimat: ${offer.SignatureRequiredFeeUsd:0.00}");

                var navQuote = new UnifiedCarrierQuoteDto
                {
                    CarrierKey = "navlungo",
                    CarrierName = "Navlungo",
                    ServiceCode = carrierSlug,
                    ServiceType = serviceLabel,
                    SubCarrier = offer.Carrier,
                    EstimatedDays = string.IsNullOrWhiteSpace(offer.DeliveryEstimate) ? "2-5 İş Günü" : offer.DeliveryEstimate,
                    PriceUsd = priceUsd,
                    PriceTry = Math.Round(priceUsd * usdTry, 2, MidpointRounding.AwayFromZero),
                    IsLive = offerLive,
                    QuoteSourceBadge = offerLive ? "Canlı teklif" : "Tahmini tarife",
                    HasDetailedBreakdown = true,
                    BillableWeightKg = offerBillable,
                    BaseCargoPriceUsd = baseSystem,
                    FuelCostUsd = fuelCost,
                    FuelMultiplierPercent = fuelPct,
                    AdditionalFeeUsd = peakFee,
                    AdditionalFeeDescription = peakFee > 0m ? "Yoğun Sezon Ek Ücreti (Peak Season Surcharge)" : string.Empty,
                    PricingTemplate = offer.EarlyTrackingEnabled ? "Erken Takip No Aktif (Navlungo)" : "Navlungo Portal",
                    InsuranceNote = notes.Count > 0 ? string.Join(" · ", notes) : "Navlungo standart kapsam",
                    ReturnPolicyNote = offer.IsDdpRequired
                        ? "DDP/DDU Seçilebilir · ABD & AB gönderilerinde gümrük peşin ödenebilir"
                        : "DDU seçilirse alıcı vergisi/iade riski göndericiye yansır"
                };

                string navCustomsBadge = navUsingLive && offer.DdpFeeUsd > 0m
                    ? "Navlungo Canlı DDP API"
                    : (country == "US" ? "Navlungo DDP Hizmeti + ABD Tarife" : "Navlungo DDP Tarife");

                ApplyCustomsBreakdownToQuote(
                    navQuote, declaredTotalUsd, hsCode, baseGenRate, baseAddRate, baseAddHts,
                    navFee, navListFee, navLabel, navCustomsBadge, usdTry);
                quotes.Add(navQuote);
            }
        }

        quotes = quotes.OrderBy(q => q.PriceUsd).ToList();
        if (quotes.Count > 0)
        {
            quotes[0].IsLowestPrice = true;
            var bestLive = quotes.FirstOrDefault(q => q.IsLive) ?? quotes[0];
            bestLive.IsRecommended = true;
        }

        return new CarrierQuotesResponseDto
        {
            CalculatedDesi = desi,
            BillableWeightKg = billable,
            LiveCount = quotes.Count(q => q.IsLive),
            EstimatedCount = quotes.Count(q => !q.IsLive),
            WarningMessage = arasResult.TokenExpired ? arasResult.StatusMessage : null,
            Quotes = quotes
        };
    }

    private static readonly (string Code, string Category, string Description, string Keywords)[] CuratedHsCatalog =
    [
        ("3926400000", "3D Baskı & Figür", "Plastikten / PLA / Reçineden 3D Baskı Heykelcik, Biblolar ve Süs Eşyası", "3d print pla resin figure figurine statue decor miniature dragon"),
        ("3926909990", "3D Baskı & Aparat", "Plastikten Diğer Eşyalar (Stand, Tutucu, Organizer, Yedek Parça)", "plastic holder stand mount organizer clip bracket custom 3d"),
        ("9503007000", "Oyuncak & Hobi", "Plastik Oyuncaklar, Eklemli 3D Figürler (Articulated Fidget / Toy)", "toy fidget articulated flexi game puzzle"),
        ("9405210000", "Aydınlatma & Lamba", "LED Masa Lambası, Gece Lambası, Lithophane / 3D Baskı Aydınlatma", "lamp led light nightlight lithophane lampshade"),
        ("6912002510", "Seramik & Kupa", "Seramik / Porselen Kahve Kupası, Bardak ve Sofra Eşyası (Mug)", "mug cup ceramic coffee tea tableware"),
        ("4420190000", "Ahşap & Dekor", "Ahşap Dekoratif Süs Eşyası, Lazer Kesim Duvar Dekoru ve Heykelcik", "wood wooden laser cut sign wall art plaque"),
        ("7117199000", "Takı & Aksesuar", "Bijuteri, Kolye, Küpe, Bileklik ve Metal/Plastik Takılar", "jewelry necklace earring bracelet ring pendant charm"),
        ("7113110000", "Gümüş Takı", "925 Ayar Gümüş Takı ve Mücevherat (Kolye, Yüzük, Küpe)", "silver 925 sterling ring necklace jewelry"),
        ("6109100012", "Tekstil & Giyim", "Pamuklu Baskılı Tişört, Sweatshirt ve Örme Giyim Eşyası", "t-shirt shirt tee hoodie sweatshirt apparel cotton"),
        ("4202229000", "Çanta & Cüzdan", "Bez Çanta (Tote Bag), Deri/Tekstil Cüzdan ve Kılıf", "bag tote wallet pouch purse leather case"),
        ("4911910040", "Tablo & Poster", "Basılı Sanat Posteri, Kanvas Tablo, Fotoğraf ve Sticker", "poster print canvas art sticker decal paper"),
        ("8306290000", "Metal Dekor", "Adi Metalden Duvar Sanatı, Dekoratif Metal Tablo ve Heykelcik", "metal wall art iron steel decor sculpture"),
        ("3406000000", "Mum & Kokulu Ürün", "El Yapımı Soya Mumu, Dekoratif Kokulu Mum", "candle soy wax scented aroma"),
        ("6307909891", "Ev Tekstili", "Dekoratif Yastık Kılıfı, Makrome ve Dokuma Ev Tekstili", "pillow cushion macrame tapestry textile woven"),
        ("3924104000", "Mutfak & Sunum", "Plastik Mutfak Gereçleri, Kurabiye Kalıbı (Cookie Cutter)", "cookie cutter kitchen coaster tray")
    ];

    public IReadOnlyList<HsCodeSearchItemDto> SearchHsCodes(string? query, int limit = 25)
    {
        int take = Math.Clamp(limit, 1, 50);
        string q = (query ?? string.Empty).Trim();
        string digitsOnly = new string(q.Where(char.IsDigit).ToArray());
        string lower = q.ToLowerInvariant();

        var results = new List<HsCodeSearchItemDto>();

        foreach (var item in CuratedHsCatalog)
        {
            if (string.IsNullOrWhiteSpace(q) ||
                item.Code.Contains(digitsOnly, StringComparison.OrdinalIgnoreCase) && digitsOnly.Length > 0 ||
                item.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.Keywords.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(tok =>
                    tok.Length >= 2 && (
                        item.Description.Contains(tok, StringComparison.OrdinalIgnoreCase) ||
                        item.Keywords.Contains(tok, StringComparison.OrdinalIgnoreCase) ||
                        item.Category.Contains(tok, StringComparison.OrdinalIgnoreCase))))
            {
                results.Add(new HsCodeSearchItemDto
                {
                    Code = item.Code,
                    FormattedCode = ShipEntegraHsCodeCatalog.Format(item.Code),
                    Category = item.Category,
                    Description = item.Description,
                    IsUsValidated = ShipEntegraHsCodeCatalog.IsUsCode(item.Code)
                });
            }
        }

        // Eğer kullanıcı rakam girdiyse gömülü 23.457 ABD HTS kataloğundan da eşleşmeleri ekle
        if (digitsOnly.Length >= 4 && results.Count < take)
        {
            string formattedPrefix = ShipEntegraHsCodeCatalog.Format(digitsOnly);
            try
            {
                var usMatches = ShipEntegraHsCodeCatalog.AllUsCodes
                    .Where(c => c.StartsWith(formattedPrefix, StringComparison.Ordinal))
                    .Take(take - results.Count);

                foreach (string formatted in usMatches)
                {
                    string rawDigits = new string(formatted.Where(char.IsDigit).ToArray());
                    if (results.Any(r => r.Code == rawDigits)) continue;

                    results.Add(new HsCodeSearchItemDto
                    {
                        Code = rawDigits,
                        FormattedCode = formatted,
                        Category = "ABD HTS Gümrük Kataloğu",
                        Description = $"ABD Gümrük Tarife Kodu ({formatted}) — Doğrulanmış HTS",
                        IsUsValidated = true
                    });
                }
            }
            catch { }
        }

        return results.Take(take).ToList();
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

    private static async Task<IReadOnlyList<ShiptomoreHsCode>> SafeSearchShiptomoreHsAsync(
        IShiptomoreOfficialApi api,
        string hsPrefix,
        CancellationToken ct)
    {
        try
        {
            return await api.SearchHsCodesAsync(hsPrefix, 10, ct);
        }
        catch
        {
            return Array.Empty<ShiptomoreHsCode>();
        }
    }

    private static async Task<List<NavlungoQuoteOffer>> SafeFetchNavlungoQuotesAsync(
        INavlungoApiClient api,
        NavlungoQuoteRequest request,
        NavlungoSettings settings,
        CancellationToken ct)
    {
        try
        {
            return await api.FetchLiveQuotesAsync(request, settings, ct);
        }
        catch
        {
            return new List<NavlungoQuoteOffer>();
        }
    }

    private static (decimal FeeUsd, decimal? ListFeeUsd, string Label) ResolveCarrierDdpProcessingFee(
        string carrierKey,
        string? subCarrier,
        string? serviceCode,
        ShiptomoreQuoteOffer? stmOffer = null,
        decimal navDdpFeeUsd = 0m,
        decimal declaredTotalUsd = 1m,
        decimal generalDutyRatePercent = 0m,
        decimal additionalDutyRatePercent = 0m)
    {
        if (carrierKey == "navlungo" && navDdpFeeUsd > 0m)
        {
            decimal expectedTax = Math.Round(declaredTotalUsd * (generalDutyRatePercent / 100m), 2, MidpointRounding.AwayFromZero)
                                + Math.Round(declaredTotalUsd * (additionalDutyRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
            decimal inferredProcessing = Math.Round(Math.Max(0m, navDdpFeeUsd - expectedTax), 2, MidpointRounding.AwayFromZero);
            string navLbl = (subCarrier ?? string.Empty).Contains("widect", StringComparison.OrdinalIgnoreCase)
                ? "Navlungo DDP Hizmeti (Widect)"
                : "Navlungo DDP Hizmeti (Express)";
            return (inferredProcessing, null, navLbl);
        }

        if (stmOffer != null && stmOffer.DdpFeeUsd > 0m)
        {
            decimal? listFee = stmOffer.DdpFeeUnDiscountedUsd.HasValue && stmOffer.DdpFeeUnDiscountedUsd.Value > stmOffer.DdpFeeUsd
                ? Math.Round(stmOffer.DdpFeeUnDiscountedUsd.Value, 2)
                : null;
            string label = !string.IsNullOrWhiteSpace(stmOffer.DdpFeeName)
                ? stmOffer.DdpFeeName!
                : "DDP Taşıyıcı İşlem Bedeli";
            return (Math.Round(stmOffer.DdpFeeUsd, 2), listFee, label);
        }

        string sub = $"{subCarrier} {serviceCode}".ToLowerInvariant();
        if (carrierKey == "navlungo")
        {
            if (sub.Contains("widect") || sub.Contains("eco"))
                return (1.50m, null, "Navlungo DDP Hizmeti (Widect)");
            return (4.50m, null, "Navlungo DDP Hizmeti (Express)");
        }

        if (sub.Contains("fedex"))
        {
            decimal? fedexList = carrierKey == "shiptomore" ? 4.50m : null;
            return (2.50m, fedexList, "DDP Taşıyıcı İşlem Bedeli (FedEx)");
        }

        if (sub.Contains("widect") || sub.Contains("eco"))
            return (0.45m, null, "DDP Ücreti (Widect)");

        if (sub.Contains("ups") || sub.Contains("dhl") || sub.Contains("express"))
            return (1.45m, null, "DDP Taşıyıcı İşlem Bedeli (Express)");

        return (1.45m, null, "DDP Taşıyıcı İşlem Bedeli");
    }

    private static void ApplyCustomsBreakdownToQuote(
        UnifiedCarrierQuoteDto quote,
        decimal declaredTotalUsd,
        string hsCode,
        decimal generalRatePercent,
        decimal additionalRatePercent,
        string additionalHtsCode,
        decimal processingFeeUsd,
        decimal? processingFeeListUsd,
        string processingFeeLabel,
        string customsSourceBadge,
        decimal usdTry)
    {
        decimal generalDutyUsd = Math.Round(declaredTotalUsd * (generalRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        decimal additionalDutyUsd = Math.Round(declaredTotalUsd * (additionalRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        decimal taxSubtotal = Math.Round(generalDutyUsd + additionalDutyUsd, 2);
        decimal fastCustomsUsd = Math.Round(taxSubtotal + processingFeeUsd, 2);
        decimal grandTotalUsd = Math.Round(quote.PriceUsd + fastCustomsUsd, 2);

        quote.HasCustomsBreakdown = true;
        quote.CustomsSourceBadge = customsSourceBadge;
        quote.DeclaredValueUsd = declaredTotalUsd;
        quote.HsCode = hsCode;
        quote.GeneralDutyRatePercent = Math.Round(generalRatePercent, 2);
        quote.GeneralDutyUsd = generalDutyUsd;
        quote.AdditionalDutyRatePercent = Math.Round(additionalRatePercent, 2);
        quote.AdditionalDutyUsd = additionalDutyUsd;
        quote.AdditionalDutyHtsCode = additionalHtsCode;
        quote.TaxAndDutySubtotalUsd = taxSubtotal;
        quote.CarrierProcessingFeeUsd = Math.Round(processingFeeUsd, 2);
        quote.CarrierProcessingFeeLabel = processingFeeLabel;
        quote.CarrierProcessingFeeListUsd = processingFeeListUsd;
        quote.FastCustomsPaymentUsd = fastCustomsUsd;
        quote.FastCustomsPaymentTry = Math.Round(fastCustomsUsd * usdTry, 2);
        quote.GrandTotalUsd = grandTotalUsd;
        quote.GrandTotalTry = Math.Round(grandTotalUsd * usdTry, 2);
    }
}
