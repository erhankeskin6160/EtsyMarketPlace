namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras Global gönderi oluşturma sağlayıcısı (IShipmentCreationProvider implementasyonu).
/// Akış, panelin GERÇEK ağ trafiğinden birebir alınmıştır:
/// taslak oluştur → bu gönderi için fiyat hesaplamayı başlat → teklif listesini bekle →
/// gönderiyi güncelle (taslak kapat, taşıyıcı seç) → fiyatı hesapla → yasal belgeler →
/// fiyat ve onayları gönder. (Ödeme adımı kapsam dışıdır; panelden yapılır.)
/// Oturum süresi dolduğunda (401) otomatik token yenileme yeteneğine sahiptir.
/// </summary>
public sealed class ArasGlobalShipmentCreationProvider : IShipmentCreationProvider
{
    private readonly IArasGlobalApiClient _apiClient;
    private readonly IShippingSessionManager? _sessionManager;
    private readonly Func<string, string>? _passwordDecryptor;
    private readonly Func<ArasGlobalSettings> _settingsProvider;

    public string ProviderName => "Aras Global";
    public bool IsCreationSupported => true;

    public ArasGlobalShipmentCreationProvider(
        IArasGlobalApiClient apiClient,
        IShippingSessionManager? sessionManager = null,
        Func<string, string>? passwordDecryptor = null,
        Func<ArasGlobalSettings>? settingsProvider = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _sessionManager = sessionManager;
        _passwordDecryptor = passwordDecryptor;
        _settingsProvider = settingsProvider ?? (() => ArasGlobalSettingsStore.Load());
    }

    public async Task<ShipmentCreationResult> CreateShipmentAsync(
        ShipmentCreationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null || context.Order == null)
        {
            return new ShipmentCreationResult
            {
                IsSuccess = false,
                ErrorMessage = "Gönderi başlamış veya Etsy sipariş bilgisi eksik."
            };
        }

        var settings = _settingsProvider();
        string token = settings.CleanToken;

        // Token geçersizse, süresi dolmuşsa veya kayıtlı hesap varsa önce otomatik yenilemeyi dene
        if (string.IsNullOrWhiteSpace(token) || token.Length <= 20 || JwtTokenInspector.IsExpired(token))
        {
            string? autoToken = await TryRefreshTokenAsync(settings, cancellationToken, knownExpiredToken: token);
            if (!string.IsNullOrWhiteSpace(autoToken))
            {
                token = autoToken;
            }
            else
            {
                return new ShipmentCreationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Aras Global oturum tokeni bulunamadı veya süresi dolmuş. Lütfen 'Aras Oturumu Yenile' butonuna basın."
                };
            }
        }

        try
        {
            return await ExecuteCreationFlowAsync(context, token, settings, cancellationToken);
        }
        catch (ArasGlobalTokenExpiredException)
        {
            // Token süresi doldu hatası (HTTP 401) alındığında arka planda bir kez otomatik tazelemeyi dene
            string? freshToken = await TryRefreshTokenAsync(settings, cancellationToken, knownExpiredToken: token);
            if (!string.IsNullOrWhiteSpace(freshToken))
            {
                try
                {
                    return await ExecuteCreationFlowAsync(context, freshToken, settings, cancellationToken);
                }
                catch (Exception retryEx)
                {
                    return new ShipmentCreationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Token yenilendi ancak gönderi oluşturulamadı: {retryEx.Message}"
                    };
                }
            }

            return new ShipmentCreationResult
            {
                IsSuccess = false,
                ErrorMessage = "Aras Global canlı oturum tokeninizin süresi dolmuş (HTTP 401). Lütfen üst bardaki 'Aras Oturumu' düğmesinden yenileyin."
            };
        }
        catch (Exception ex)
        {
            return new ShipmentCreationResult
            {
                IsSuccess = false,
                ErrorMessage = $"Aras Global gönderi oluşturma hatası: {ex.Message}"
            };
        }
    }

    private async Task<ShipmentCreationResult> ExecuteCreationFlowAsync(
        ShipmentCreationContext context,
        string token,
        ArasGlobalSettings settings,
        CancellationToken cancellationToken)
    {
        var order = context.Order;

        // 1. Kutu ve ebat bilgileri
        double length = context.LengthCm > 0 ? context.LengthCm : 20.0;
        double width = context.WidthCm > 0 ? context.WidthCm : 15.0;
        double height = context.HeightCm > 0 ? context.HeightCm : 10.0;
        double weight = context.WeightKg > 0 ? context.WeightKg : 0.4;

        // 2. Ürün kalemleri (Etsy siparişinden)
        string hsCode = !string.IsNullOrWhiteSpace(context.HsCode)
            ? context.HsCode
            : (order.Items.Count > 0 && !string.IsNullOrWhiteSpace(order.Items[0].HsCode) ? order.Items[0].HsCode : "3926400000");

        decimal itemsTotal = order.Items.Count > 0
            ? order.Items.Sum(i => i.Price * Math.Max(1, i.Quantity))
            : 0m;
        decimal orderPrice = itemsTotal > 0 ? itemsTotal : (order.TotalPrice > 0 ? order.TotalPrice : 11.0m);

        string providerName = NormalizeProviderName(context.SelectedSubCarrier);
        string currency = string.IsNullOrWhiteSpace(order.Currency) ? "USD" : order.Currency;

        var contentItems = new List<ArasShipmentContentItem>();
        foreach (var itm in order.Items)
        {
            string desc = !string.IsNullOrWhiteSpace(itm.Title) ? itm.Title : "E-Ticaret Ürünü";
            int qty = itm.Quantity > 0 ? itm.Quantity : 1;
            contentItems.Add(new ArasShipmentContentItem
            {
                Description = desc,
                HsCode = !string.IsNullOrWhiteSpace(itm.HsCode) ? itm.HsCode : hsCode,
                ProductBarcode = Guid.NewGuid().ToString(),
                Disabled = false,
                Quantity = qty,
                Amount = qty,
                UnitPrice = itm.Price > 0 ? itm.Price : orderPrice,
                ManufacturerCountry = "TR",
                MaxDigitalValue = 0
            });
        }

        if (contentItems.Count == 0)
        {
            contentItems.Add(new ArasShipmentContentItem
            {
                Description = "3d print figür",
                HsCode = hsCode,
                ProductBarcode = Guid.NewGuid().ToString(),
                Disabled = false,
                Quantity = 1,
                Amount = 1,
                UnitPrice = orderPrice,
                ManufacturerCountry = "TR",
                MaxDigitalValue = 0
            });
        }

        // 3. Gönderici adresi: panelde kayıtlı adres (GetAddresses). Alınamazsa güvenli varsayılan.
        var sender = await _apiClient.GetPrimarySenderAddressAsync(token, cancellationToken);
        if (sender == null || string.IsNullOrWhiteSpace(sender.CityName))
        {
            sender = BuildFallbackSender(settings);
        }

        // 4. Alıcı adresi (Etsy siparişinden)
        string buyerFirst = order.BuyerName;
        string buyerLast = string.Empty;
        int spaceIdx = order.BuyerName.LastIndexOf(' ');
        if (spaceIdx > 0)
        {
            buyerFirst = order.BuyerName.Substring(0, spaceIdx);
            buyerLast = order.BuyerName.Substring(spaceIdx + 1);
        }

        var receiver = new ArasReceiverAddress
        {
            Title = string.Empty,
            FirstName = buyerFirst,
            LastName = buyerLast,
            CityName = order.City,
            TownName = string.Empty,
            PhoneCountryCode = null,
            CountryCode = !string.IsNullOrWhiteSpace(order.CountryCode) ? order.CountryCode : "US",
            StateCode = order.HasState ? (order.State ?? string.Empty) : string.Empty,
            StateName = order.HasState ? (order.State ?? string.Empty) : string.Empty,
            PhoneNumber = order.Phone ?? string.Empty,
            PostalCode = order.PostalCode,
            Email = order.BuyerEmail,
            CompanyName = string.Empty,
            Details = !string.IsNullOrWhiteSpace(order.StreetAddress) ? order.StreetAddress : "Delivery Address",
            Details2 = string.Empty,
            Type = 2
        };

        var request = new ArasCreateShipmentRequest
        {
            SenderAddress = sender,
            BillingAddress = null,
            ReceiverAddress = receiver,
            PieceCount = 1,
            InternationalShipmentCategory = "0", // Panel sözleşmesi (Numune); içerik ticari beyanı Contents ile yapılır.
            Contents = new List<ArasShipmentContent>
            {
                new ArasShipmentContent
                {
                    EstimatedDimensions = new ArasEstimatedDimensions
                    {
                        Length = length,
                        Width = width,
                        Height = height,
                        Weight = weight
                    },
                    Items = contentItems
                }
            },
            Currency = currency,
            IsDraftShipment = true,
            PackageType = 1,
            SenderBillingAddress = sender
        };

        // Adım 1: Gönderi taslağını oluştur
        var createResponse = await _apiClient.CreateShipmentAsync(request, token, cancellationToken);
        if (createResponse == null || !createResponse.IsSuccess || string.IsNullOrWhiteSpace(createResponse.ShipmentId))
        {
            return new ShipmentCreationResult
            {
                IsSuccess = false,
                ErrorMessage = $"Aras Global gönderi taslağı oluşturulamadı: {createResponse?.ResultMessage ?? "Bilinmeyen API hatası"}"
            };
        }

        string shipmentId = createResponse.ShipmentId;
        string referenceCode = !string.IsNullOrWhiteSpace(createResponse.ReferenceCode) ? createResponse.ReferenceCode : shipmentId;

        // Adım 2: Bu gönderi için canlı fiyat hesaplamayı başlat ve teklif listesini bekle
        var startPayload = new ArasStartCalculationPayload
        {
            Currency = currency,
            InternationalShipmentCategory = "0",
            IsIndividualCustomer = true,
            IsMicroExport = true,
            PackageCount = 1,
            PackageType = 1,
            ReceiverCity = receiver.CityName,
            ReceiverCountry = receiver.CountryCode,
            ReceiverPostalCode = receiver.PostalCode,
            ReceiverState = receiver.StateName,
            ReceiverTown = receiver.TownName,
            SenderCountry = sender.CountryCode,
            SenderPostalCode = sender.PostalCode,
            SenderCity = sender.CityName,
            SenderState = string.Empty,
            SenderTown = sender.TownName,
            TotalPrice = orderPrice,
            ShipmentId = shipmentId,
            ShipmentDimensions =
            {
                new ArasStartCalculationBox
                {
                    Length = length,
                    Width = width,
                    Height = height,
                    Weight = weight,
                    PackageCount = 1,
                    ShipmentItems = contentItems.ConvertAll(i => new ArasStartCalculationItem
                    {
                        Quantity = i.Quantity,
                        HsCode = i.HsCode,
                        UnitPrice = i.UnitPrice
                    })
                }
            }
        };

        await _apiClient.StartPriceCalculationForShipmentAsync(startPayload, token, cancellationToken);

        for (int attempt = 1; attempt <= 10; attempt++)
        {
            await Task.Delay(1000, cancellationToken);
            var (isConcluded, _) = await _apiClient.PollBasePriceListAsync(shipmentId, token, cancellationToken);
            if (isConcluded)
            {
                break;
            }
        }

        // Adım 3: Gönderiyi güncelle (taslağı kapat, seçilen taşıyıcıyı işle)
        request.IsDraftShipment = false;
        request.ShipmentId = shipmentId;
        request.InternationalCargoProvider = providerName;
        await _apiClient.UpdateShipmentAsync(request, token, cancellationToken);

        // Adım 4: Nihai fiyatı hesapla
        var breakdown = await _apiClient.CalculateShipmentPriceAsync(shipmentId, providerName, token, cancellationToken);

        // Adım 5: Yasal sözleşme belgelerini al (onay öncesi)
        try
        {
            await _apiClient.GetShipmentLegalDocumentAsync(shipmentId, "ShipmentAgreement", token, cancellationToken);
            await _apiClient.GetShipmentLegalDocumentAsync(shipmentId, "ShipmentInformationForm", token, cancellationToken);
        }
        catch (Exception docEx)
        {
            ArasGlobalSettingsStore.LogTrace("LegalDoc-Warning", docEx.Message);
        }

        // Adım 6: Fiyat ve onayları Aras Global'e gönder (ödeme adımı panelde tamamlanır)
        await _apiClient.SendShipmentPriceAsync(shipmentId, providerName, breakdown.TotalPrice, token, cancellationToken);

        return new ShipmentCreationResult
        {
            IsSuccess = true,
            ShipmentId = shipmentId,
            TrackingNumber = referenceCode,
            LabelUrl = $"https://panel.arasglobalcargo.com/order/barcode/{shipmentId}",
            PriceBreakdown = breakdown
        };
    }

    /// <summary>Panel sözleşmesindeki taşıyıcı adı biçimi (widect → Widect, ups → UPS).</summary>
    private static string NormalizeProviderName(string? subCarrier)
    {
        string p = (subCarrier ?? string.Empty).Trim();
        return p.ToLowerInvariant() switch
        {
            "widect" or "widex" => "Widect",
            "ups" => "UPS",
            "" => "Widect",
            _ => char.ToUpperInvariant(p[0]) + p.Substring(1)
        };
    }

    /// <summary>Panel adresi alınamazsa kullanılan güvenli varsayılan gönderici.</summary>
    private static ArasShipmentSenderAddress BuildFallbackSender(ArasGlobalSettings settings)
    {
        string senderPhone = "05342600561";
        if (!string.IsNullOrWhiteSpace(settings.SavedEmail) && settings.SavedEmail.All(char.IsDigit))
        {
            senderPhone = settings.SavedEmail.StartsWith("0") ? settings.SavedEmail : "0" + settings.SavedEmail;
        }

        string senderEmail = !string.IsNullOrWhiteSpace(settings.SavedEmail) && settings.SavedEmail.Contains("@")
            ? settings.SavedEmail
            : "erhankeskin6160@gmail.com";

        return new ArasShipmentSenderAddress
        {
            Title = "Merkez",
            FirstName = "ERHAN KESKİN",
            LastName = string.Empty,
            CityName = "ANKARA",
            TownName = "ALTINDAĞ",
            Details = "Ankara Altındağ",
            CountryName = "Turkiye",
            CountryCode = "TR",
            PostalCode = "06350",
            PhoneNumber = senderPhone,
            Email = senderEmail
        };
    }

    private async Task<string?> TryRefreshTokenAsync(
        ArasGlobalSettings settings,
        CancellationToken cancellationToken,
        string? knownExpiredToken = null)
    {
        if (_sessionManager == null || string.IsNullOrWhiteSpace(settings.SavedEmail))
            return null;

        try
        {
            string pass = (!string.IsNullOrWhiteSpace(settings.EncryptedPassword) && _passwordDecryptor != null)
                ? _passwordDecryptor(settings.EncryptedPassword)
                : string.Empty;

            string? freshToken = await _sessionManager.RefreshArasGlobalTokenAsync(
                settings.SavedEmail,
                pass,
                showBrowser: false,
                knownExpiredToken: knownExpiredToken ?? settings.CleanToken,
                ct: cancellationToken);

            if (!string.IsNullOrWhiteSpace(freshToken))
            {
                string clean = freshToken.Trim();
                if (clean.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(7).Trim();
                }
                settings.BearerToken = clean;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                ArasGlobalSettingsStore.Save(settings);
                return clean;
            }
        }
        catch
        {
            // Otomatik yenileme sessiz hata
        }

        return null;
    }
}
