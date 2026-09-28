namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.IO;
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

        var labelRequest = new ShipEntegraCreateLabelRequest
        {
            SpecialService = !string.IsNullOrWhiteSpace(context.SelectedSubCarrier) ? context.SelectedSubCarrier! : "shipentegra-express",
            Content = description,
            Weight = weight,
            IossNumber = order.IossNumber ?? string.Empty,
            Currency = currency,
            Items = labelItems,
            OrderId = orderResult.OrderId,
            ServiceType = 1,
            Country = request.ShipTo.Country,
            Insurance = false,
            NoTracking = false,
            Verpackg = -1
        };

        byte[]? labelBytes = null;
        try
        {
            labelBytes = await _apiClient.CreateLabelAsync(labelRequest, token, cancellationToken);
        }
        catch (Exception)
        {
            // Sipariş oluştu; etiket daha sonra panelden de alınabilir.
        }

        string labelPath = string.Empty;
        if (labelBytes != null && labelBytes.Length > 0)
        {
            try
            {
                string labelDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimilarProductsWinForms",
                    "labels");
                Directory.CreateDirectory(labelDir);
                bool isPdf = labelBytes.Length > 4 &&
                             labelBytes[0] == 0x25 && labelBytes[1] == 0x50 &&
                             labelBytes[2] == 0x44 && labelBytes[3] == 0x46;
                string ext = isPdf ? "pdf" : "bin";
                labelPath = Path.Combine(labelDir, $"shipentegra-{orderResult.OrderId}.{ext}");
                File.WriteAllBytes(labelPath, labelBytes);
            }
            catch (Exception)
            {
                labelPath = string.Empty;
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

    private async Task<string?> TryRefreshTokenAsync(ShipEntegraSettings settings, CancellationToken cancellationToken)
    {
        if (_sessionManager == null || string.IsNullOrWhiteSpace(settings.SavedEmail))
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
        catch (Exception)
        {
            // Sessiz yenileme hatası; üst katman bilgilendirir.
        }

        return null;
    }

    private static ShipmentCreationResult Fail(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
