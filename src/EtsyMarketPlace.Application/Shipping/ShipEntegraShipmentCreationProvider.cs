namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// ShipEntegra gönderi oluşturma sağlayıcısı.
/// Akış, panelin GERÇEK ağ trafiğinden birebir alınmıştır:
/// sipariş oluştur (/v1/orders) → kalem kimliklerini doğrula → etiket oluştur
/// (/v1/logistics/labels/shipentegra). Etiket PDF'i yerelde saklanır.
/// Ödeme adımı kapsam dışıdır; panelin kendi ödeme/bakiye modeliyle yürür.
/// </summary>
public sealed class ShipEntegraShipmentCreationProvider : IShipmentCreationProvider
{
    private readonly IShipEntegraApiClient? _apiClient;
    private readonly IShippingSessionManager? _sessionManager;
    private readonly Func<string, string>? _passwordDecryptor;
    private readonly Func<ShipEntegraSettings> _settingsProvider;

    public string ProviderName => "ShipEntegra";
    public bool IsCreationSupported => true;

    public ShipEntegraShipmentCreationProvider(
        IShipEntegraApiClient? apiClient = null,
        IShippingSessionManager? sessionManager = null,
        Func<string, string>? passwordDecryptor = null,
        Func<ShipEntegraSettings>? settingsProvider = null)
    {
        _apiClient = apiClient;
        _sessionManager = sessionManager;
        _passwordDecryptor = passwordDecryptor;
        _settingsProvider = settingsProvider ?? (() => ShipEntegraSettingsStore.Load());
    }

    public async Task<ShipmentCreationResult> CreateShipmentAsync(
        ShipmentCreationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null || context.Order == null)
        {
            return Fail("Gönderi bağlamı veya Etsy sipariş bilgisi eksik.");
        }

        if (_apiClient == null)
        {
            return Fail("ShipEntegra API istemcisi yapılandırılmamış.");
        }

        var settings = _settingsProvider();
        string token = settings.CleanToken;

        if (string.IsNullOrWhiteSpace(token) || token.Length <= 20)
        {
            string? fresh = await TryRefreshTokenAsync(settings, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fresh))
            {
                token = fresh;
            }
            else
            {
                return Fail("ShipEntegra oturum tokeni bulunamadı. Lütfen oturum düğmesinden giriş yapın.");
            }
        }

        try
        {
            return await ExecuteAsync(context, token!, cancellationToken);
        }
        catch (ShipEntegraTokenExpiredException)
        {
            string? fresh = await TryRefreshTokenAsync(settings, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fresh))
            {
                try
                {
                    return await ExecuteAsync(context, fresh, cancellationToken);
                }
                catch (Exception retryEx)
                {
                    return Fail($"Token yenilendi ancak gönderi oluşturulamadı: {retryEx.Message}");
                }
            }

            return Fail("ShipEntegra oturumunuzun süresi dolmuş. Lütfen yeniden giriş yapın.");
        }
        catch (ShipEntegraBusinessException bex)
        {
            return Fail($"ShipEntegra isteği reddedildi: {bex.Description} ({bex.Code})");
        }
        catch (Exception ex)
        {
            return Fail($"ShipEntegra gönderi oluşturma hatası: {ex.Message}");
        }
    }

    private async Task<ShipmentCreationResult> ExecuteAsync(
        ShipmentCreationContext context,
        string token,
        CancellationToken cancellationToken)
    {
        var order = context.Order;

        double length = context.LengthCm > 0 ? context.LengthCm : 20.0;
        double width = context.WidthCm > 0 ? context.WidthCm : 15.0;
        double height = context.HeightCm > 0 ? context.HeightCm : 10.0;
        double weight = context.WeightKg > 0 ? context.WeightKg : 0.4;
        string currency = string.IsNullOrWhiteSpace(order.Currency) ? "USD" : order.Currency;

        string hsCode = !string.IsNullOrWhiteSpace(context.HsCode)
            ? context.HsCode!
            : (order.Items.Count > 0 && !string.IsNullOrWhiteSpace(order.Items[0].HsCode) ? order.Items[0].HsCode! : "0302530000");

        var products = new List<ShipEntegraOrderProduct>();
        foreach (var itm in order.Items)
        {
            products.Add(new ShipEntegraOrderProduct
            {
                Name = !string.IsNullOrWhiteSpace(itm.Title) ? itm.Title : "Etsy Order Item",
                Quantity = itm.Quantity > 0 ? itm.Quantity : 1,
                UnitPrice = itm.Price > 0 ? itm.Price : (order.TotalPrice > 0 ? order.TotalPrice : 11.0m),
                HsCode = string.IsNullOrWhiteSpace(itm.HsCode) ? hsCode : itm.HsCode!
            });
        }

        if (products.Count == 0)
        {
            products.Add(new ShipEntegraOrderProduct
            {
                Name = "Etsy Order Item",
                Quantity = 1,
                UnitPrice = order.TotalPrice > 0 ? order.TotalPrice : 11.0m,
                HsCode = hsCode
            });
        }

        if (string.Equals(order.CountryCode, "US", StringComparison.OrdinalIgnoreCase))
        {
            var invalidCodes = products
                .Select(p => p.HsCode)
                .Where(code => !ShipEntegraHsCodeCatalog.IsUsCode(code))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (invalidCodes.Count > 0)
            {
                string suggestions = ShipEntegraHsCodeCatalog.SuggestFor(invalidCodes[0]);
                string hint = string.IsNullOrEmpty(suggestions)
                    ? string.Empty
                    : $" Benzer geçerli kodlar: {suggestions}.";
                throw new InvalidOperationException(
                    "ABD gönderilerinde HS kodu, ShipEntegra ABD tarife listesinden (HTS) seçilmelidir. " +
                    $"Geçersiz: {string.Join(", ", invalidCodes)}.{hint} Kodu düzeltip yeniden deneyin.");
            }
        }

        string description = products[0].Name;
        if (description.Length > 60)
        {
            description = description.Substring(0, 60);
        }

        var request = new ShipEntegraCreateOrderRequest
        {
            RememberSenderAddress = false,
            RememberShipToContact = false,
            ShipTo = new ShipEntegraShipTo
            {
                Name = string.IsNullOrWhiteSpace(order.BuyerName) ? "-" : order.BuyerName,
                Address1 = !string.IsNullOrWhiteSpace(order.StreetAddress) ? order.StreetAddress : "-",
                City = string.IsNullOrWhiteSpace(order.City) ? "-" : order.City,
                State = order.HasState ? (order.State ?? string.Empty) : null,
                ZipCode = string.IsNullOrWhiteSpace(order.PostalCode) ? "-" : order.PostalCode,
                Country = !string.IsNullOrWhiteSpace(order.CountryCode) ? order.CountryCode : "US",
                Email = string.IsNullOrWhiteSpace(order.BuyerEmail) ? null : order.BuyerEmail
            },
            Currency = currency,
            Reference = order.ReceiptId.ToString(),
            ShippingType = ShipEntegraShippingTypeResolver.Resolve(order.CountryCode),
            Description = description,
            Products = products,
            Packages = new List<ShipEntegraOrderPackage>
            {
                new ShipEntegraOrderPackage
                {
                    PackageQuantity = 1,
                    Weight = weight,
                    Width = width,
                    Length = length,
                    Height = height
                }
            }
        };

        var orderResult = await _apiClient!.CreateOrderAsync(request, token, cancellationToken);
        if (orderResult.OrderId == 0)
        {
            return Fail("ShipEntegra sipariş kimliği alınamadı. Yanıt: " + orderResult.RawJson);
        }

        if (orderResult.ItemIds.Count < products.Count)
        {
            try
            {
                var details = await _apiClient.GetOrderItemsAsync(orderResult.OrderId, token, cancellationToken);
                if (details.Count > orderResult.ItemIds.Count)
                {
                    orderResult.ItemIds = details;
                }
            }
            catch (Exception)
            {
                // Kalem kimlikleri eksik kalırsa etiket adımı anlamlı bir hata üretir.
            }
        }

        if (orderResult.ItemIds.Count == 0)
        {
            return Fail("ShipEntegra sipariş kalem kimlikleri alınamadı; etiket oluşturulamaz. Sipariş no: " + orderResult.OrderId);
        }

        var labelItems = new List<ShipEntegraLabelItem>();
        for (int i = 0; i < products.Count; i++)
        {
            long itemId = i < orderResult.ItemIds.Count ? orderResult.ItemIds[i] : orderResult.ItemIds[0];
            labelItems.Add(new ShipEntegraLabelItem
            {
                ItemId = itemId,
                DeclaredPrice = products[i].UnitPrice,
                DeclaredQuantity = products[i].Quantity,
                OrderId = orderResult.OrderId,
                Gtip = products[i].HsCode
            });
        }

        string specialServiceCode = ShipEntegraLabelServiceCodes.Resolve(context.SelectedServiceCode, context.SelectedSubCarrier) ?? string.Empty;
        var labelRequest = new ShipEntegraCreateLabelRequest
        {
            SpecialService = specialServiceCode,
            Content = description,
            Weight = weight,
            IossNumber = order.IossNumber ?? string.Empty,
            Currency = currency,
            Items = labelItems,
            OrderId = orderResult.OrderId,
            ServiceType = ShipEntegraLabelServiceCodes.ResolveServiceType(specialServiceCode),
            Country = request.ShipTo.Country,
            Insurance = false,
            NoTracking = false,
            Verpackg = -1
        };

        byte[]? labelBytes = null;
        string labelRemoteUrl = string.Empty;
        try
        {
            byte[]? labelResponseBytes = await _apiClient.CreateLabelAsync(labelRequest, token, cancellationToken);
            (labelBytes, labelRemoteUrl) = await ResolveLabelPdfAsync(labelResponseBytes, cancellationToken);
        }
        catch (Exception)
        {
            // Sipariş oluştu; etiket 'Etiketi Önizle' ile yeniden alınabilir.
        }

        // Panel sözleşmesi: 200 yanıtta etiket oluşmuştur (data.label URL'si gelir).
        // İndirme başarısız olsa bile etiket alınmış sayılır; çift etiket alınmaz.
        bool labelSucceeded = labelBytes is { Length: > 0 } || !string.IsNullOrEmpty(labelRemoteUrl);
        if (!labelSucceeded)
        {
            ShipEntegraPendingLabelStore.Save(
                order.ReceiptId,
                orderResult.OrderId,
                JsonSerializer.Serialize(labelRequest),
                labelRequest.SpecialService);
        }

        string labelPath = string.Empty;
        if (labelSucceeded)
        {
            try
            {
                labelPath = labelBytes is { Length: > 0 }
                    ? SaveLabelBytes(orderResult.OrderId, labelBytes!)
                    : labelRemoteUrl;
            }
            catch (Exception)
            {
                labelPath = labelRemoteUrl;
            }
        }

        return new ShipmentCreationResult
        {
            IsSuccess = true,
            ShipmentId = orderResult.OrderId.ToString(),
            TrackingNumber = orderResult.OrderId.ToString(),
            LabelUrl = labelPath,
            PriceBreakdown = null
        };
    }

    /// <summary>
    /// Gönderisi oluşturulmuş ancak etiketi alınamamış siparişin etiketini yeniden dener.
    /// Sipariş çoğaltılmaz; yalnızca bekleyen etiket isteği tekrar gönderilir.
    /// </summary>
    public async Task<ShipmentCreationResult> RetryLabelAsync(
        long receiptId,
        CancellationToken cancellationToken = default)
    {
        if (_apiClient == null)
        {
            return Fail("ShipEntegra API istemcisi yapılandırılmamış.");
        }

        if (!ShipEntegraPendingLabelStore.TryLoad(receiptId, out long orderId, out string requestJson))
        {
            return Fail("Bu sipariş için bekleyen etiket kaydı bulunamadı.");
        }

        ShipEntegraCreateLabelRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<ShipEntegraCreateLabelRequest>(requestJson);
        }
        catch (Exception)
        {
            request = null;
        }

        if (request == null)
        {
            return Fail("Bekleyen etiket isteği okunamadı.");
        }

        if (string.IsNullOrWhiteSpace(request.SpecialService))
        {
            return Fail("Bu servis için etiket kodu tanımlı değil; kod tanımlandığında tekrar deneyin.");
        }

        var settings = _settingsProvider();
        string token = settings.CleanToken;
        if (string.IsNullOrWhiteSpace(token) || token.Length <= 20)
        {
            string? fresh = await TryRefreshTokenAsync(settings, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fresh))
            {
                token = fresh;
            }
            else
            {
                return Fail("ShipEntegra oturum tokeni bulunamadı. Lütfen oturum düğmesinden giriş yapın.");
            }
        }

        try
        {
            (byte[]? labelPdf, string labelUrl) = await SendLabelAsync(request, token, cancellationToken);
            return SaveRetriedLabel(receiptId, orderId, labelPdf, labelUrl);
        }
        catch (ShipEntegraTokenExpiredException)
        {
            string? fresh = await TryRefreshTokenAsync(settings, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fresh))
            {
                try
                {
                    (byte[]? labelPdf, string labelUrl) = await SendLabelAsync(request, fresh, cancellationToken);
                    return SaveRetriedLabel(receiptId, orderId, labelPdf, labelUrl);
                }
                catch (Exception retryEx)
                {
                    return Fail($"Etiket yeniden alınamadı: {retryEx.Message}");
                }
            }

            return Fail("ShipEntegra oturumunuzun süresi dolmuş. Lütfen yeniden giriş yapın.");
        }
        catch (ShipEntegraBusinessException bex)
        {
            return Fail($"ShipEntegra etiketi reddedildi: {bex.Description} ({bex.Code})");
        }
        catch (Exception ex)
        {
            return Fail($"ShipEntegra etiketi alınamadı: {ex.Message}");
        }
    }

    private async Task<(byte[]? Bytes, string RemoteUrl)> SendLabelAsync(
        ShipEntegraCreateLabelRequest request,
        string token,
        CancellationToken cancellationToken)
    {
        byte[]? responseBytes = await _apiClient!.CreateLabelAsync(request, token, cancellationToken);
        (byte[]? pdfBytes, string remoteUrl) = await ResolveLabelPdfAsync(responseBytes, cancellationToken);
        if (pdfBytes == null && string.IsNullOrEmpty(remoteUrl))
        {
            throw new InvalidOperationException("Etiket yanıtı boş veya tanınmayan biçimde döndü.");
        }

        return (pdfBytes, remoteUrl);
    }

    /// <summary>
    /// Etiket yanıtını çözümler: yanıt JSON ise data.label URL'sinden gerçek PDF'i indirir;
    /// yanıt doğrudan PDF ise olduğu gibi kullanır. İndirme başarısız olsa bile etiket
    /// sunucuda oluştuğu için uzak URL ile devam edilir (çift etiket alınmaz).
    /// </summary>
    private async Task<(byte[]? Pdf, string RemoteUrl)> ResolveLabelPdfAsync(
        byte[]? labelResponseBytes,
        CancellationToken cancellationToken)
    {
        string remoteUrl = ShipEntegraLabelResponseParser.TryExtractLabelFileUrl(labelResponseBytes) ?? string.Empty;
        if (!string.IsNullOrEmpty(remoteUrl) && _apiClient != null)
        {
            try
            {
                byte[]? pdf = await _apiClient.DownloadLabelFileAsync(remoteUrl, cancellationToken);
                if (ShipEntegraLabelResponseParser.IsPdf(pdf))
                {
                    return (pdf, remoteUrl);
                }
            }
            catch (Exception)
            {
                // İndirme başarısız; uzak URL ile devam edilir.
            }
        }

        return (ShipEntegraLabelResponseParser.IsPdf(labelResponseBytes) ? labelResponseBytes : null, remoteUrl);
    }

    private static ShipmentCreationResult SaveRetriedLabel(long receiptId, long orderId, byte[]? bytes, string remoteUrl)
    {
        if ((bytes == null || bytes.Length == 0) && string.IsNullOrEmpty(remoteUrl))
        {
            throw new InvalidOperationException("Etiket yanıtı boş döndü.");
        }

        string path;
        if (bytes is { Length: > 0 })
        {
            try
            {
                path = SaveLabelBytes(orderId, bytes);
            }
            catch (Exception)
            {
                path = remoteUrl;
            }
        }
        else
        {
            path = remoteUrl;
        }

        ShipEntegraPendingLabelStore.Delete(receiptId);
        return new ShipmentCreationResult
        {
            IsSuccess = true,
            ShipmentId = orderId.ToString(),
            TrackingNumber = orderId.ToString(),
            LabelUrl = path
        };
    }

    private static string SaveLabelBytes(long orderId, byte[] bytes)
    {
        string labelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimilarProductsWinForms",
            "labels");
        Directory.CreateDirectory(labelDir);
        bool isPdf = bytes.Length > 4 &&
                     bytes[0] == 0x25 && bytes[1] == 0x50 &&
                     bytes[2] == 0x44 && bytes[3] == 0x46;
        string ext = isPdf ? "pdf" : "bin";
        string labelPath = Path.Combine(labelDir, $"shipentegra-{orderId}.{ext}");
        File.WriteAllBytes(labelPath, bytes);
        return labelPath;
    }

    private async Task<string?> TryRefreshTokenAsync(ShipEntegraSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.SavedEmail))
        {
            return null;
        }

        // 1) API ile doğrudan giriş: tarayıcısız, en hızlı ve en güvenilir yol.
        if (_apiClient != null &&
            !string.IsNullOrWhiteSpace(settings.EncryptedPassword) &&
            _passwordDecryptor != null)
        {
            try
            {
                string pass = _passwordDecryptor(settings.EncryptedPassword);
                if (!string.IsNullOrWhiteSpace(pass))
                {
                    var tokens = await _apiClient.LoginAsync(settings.SavedEmail, pass, cancellationToken);
                    if (tokens != null && !string.IsNullOrWhiteSpace(tokens.AccessToken))
                    {
                        settings.BearerToken = tokens.AccessToken;
                        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                        if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
                        {
                            settings.RefreshToken = tokens.RefreshToken;
                            settings.RefreshTokenLastUpdatedUtc = DateTime.UtcNow;
                        }

                        ShipEntegraSettingsStore.Save(settings);
                        return tokens.AccessToken;
                    }
                }
            }
            catch (Exception caught)
            {
                AppLog.Swallowed(caught, "ShipEntegraShipmentCreationProvider.TryRefreshTokenAsync.ApiLogin");
            }
        }

        // 2) Yedek yol: görünmez tarayıcı oturumu üzerinden yakalama.
        if (_sessionManager == null)
        {
            return null;
        }

        try
        {
            string pass = (!string.IsNullOrWhiteSpace(settings.EncryptedPassword) && _passwordDecryptor != null)
                ? _passwordDecryptor(settings.EncryptedPassword)
                : string.Empty;

            string? fresh = await _sessionManager.RefreshShipEntegraTokenAsync(
                settings.SavedEmail,
                pass,
                showBrowser: false,
                ct: cancellationToken);

            if (!string.IsNullOrWhiteSpace(fresh))
            {
                string clean = fresh.Trim();
                if (clean.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(7).Trim();
                }

                settings.BearerToken = clean;
                settings.TokenLastUpdatedUtc = DateTime.UtcNow;
                ShipEntegraSettingsStore.Save(settings);
                return clean;
            }
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught, "ShipEntegraShipmentCreationProvider.TryRefreshTokenAsync.SessionManager");
        }

        return null;
    }

    private static ShipmentCreationResult Fail(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
