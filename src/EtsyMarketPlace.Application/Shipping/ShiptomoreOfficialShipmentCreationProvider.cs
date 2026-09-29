namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Diagnostics;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Ship to More resmî API'si üzerinden GERÇEK gönderi oluşturan sağlayıcı.
/// Eski <c>ShiptomoreShipmentCreationProvider</c> stub'ının yerini alır (o sınıf
/// <c>IsCreationSupported =&gt; false</c> döndüğü için arayüzde "Gönderi kapalı" görünüyordu).
///
/// Eşleştirme kararları:
/// - <c>shipment_type</c> her zaman "sale" (API varsayılanı "sample" — numune beyanı olurdu).
/// - Etsy IOSS numarası <c>collect_id_number</c> alanına yazılır.
/// - Zorunlu alanlar (telefon, e-posta, sokak, şehir, posta kodu) önce doğrulanır; eksikse
///   kullanıcıya hangi alanın eksik olduğu söylenir, boşa API çağrısı yapılmaz.
/// </summary>
public sealed class ShiptomoreOfficialShipmentCreationProvider : IShipmentCreationProvider
{
    private readonly IShiptomoreOfficialApi _api;

    public ShiptomoreOfficialShipmentCreationProvider(IShiptomoreOfficialApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public string ProviderName => "Shiptomore";

    public bool IsCreationSupported => true;

    public async Task<ShipmentCreationResult> CreateShipmentAsync(
        ShipmentCreationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_api.HasCredentials)
        {
            return Fail("Ship to More API kimlik bilgileri tanımlı değil. Ayarlar bölümünden Client ID ve Client Secret girilmeli.");
        }

        var order = context.Order;
        if (order == null)
        {
            return Fail("Sipariş bilgisi bulunamadı.");
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(order.BuyerName)) missing.Add("alıcı adı");
        if (string.IsNullOrWhiteSpace(order.StreetAddress)) missing.Add("adres");
        if (string.IsNullOrWhiteSpace(order.City)) missing.Add("şehir");
        if (string.IsNullOrWhiteSpace(order.PostalCode)) missing.Add("posta kodu");
        if (string.IsNullOrWhiteSpace(order.CountryCode)) missing.Add("ülke");
        if (string.IsNullOrWhiteSpace(order.BuyerEmail)) missing.Add("alıcı e-postası");
        if (string.IsNullOrWhiteSpace(order.Phone)) missing.Add("alıcı telefonu");

        if (missing.Count > 0)
        {
            return Fail("Ship to More için zorunlu alanlar eksik: " + string.Join(", ", missing) +
                        ". Sipariş verisinde bu alanlar boş olamaz.");
        }

        var request = BuildRequest(context);

        if (!string.IsNullOrWhiteSpace(context.HsCode))
        {
            var resolution = await ShiptomoreHsCodeResolver
                .ResolveAsync(context.HsCode, _api, cancellationToken)
                .ConfigureAwait(false);

            if (!resolution.IsResolved)
            {
                return Fail(resolution.ErrorMessage);
            }

            if (!string.Equals(resolution.Code, context.HsCode.Trim(), StringComparison.Ordinal))
            {
                AppLog.Info(
                    $"Ship to More GTİP dönüşümü: {context.HsCode.Trim()} -> {resolution.Code} (sağlayıcı kod listesinden)",
                    "ShiptomoreGtipResolve");

                foreach (var line in request.ProductLines)
                {
                    line.HsCode = resolution.Code;
                }
            }
        }

        try
        {
            var response = await _api.CreateShipmentAsync(request, cancellationToken).ConfigureAwait(false);

            return new ShipmentCreationResult
            {
                IsSuccess = true,
                ShipmentId = response.Id,
                TrackingNumber = response.TrackingNumbers.FirstOrDefault() ?? string.Empty,
                LabelUrl = string.Empty, // etiket PDF'i DownloadLabelAsync ile alınır
                ErrorMessage = string.Empty
            };
        }
        catch (ShiptomoreApiException ex)
        {
            return Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return Fail("Ship to More gönderi oluşturma hatası: " + ex.Message);
        }
    }

    /// <summary>Sipariş bağlamını şartnamedeki gönderi isteğine çevirir (test edilebilir, saf).</summary>
    public static ShiptomoreShipmentRequest BuildRequest(ShipmentCreationContext context)
    {
        var order = context.Order;
        double weight = context.WeightKg > 0 ? context.WeightKg : 0.4;
        double length = context.LengthCm > 0 ? context.LengthCm : 20;
        double width = context.WidthCm > 0 ? context.WidthCm : 15;
        double height = context.HeightCm > 0 ? context.HeightCm : 10;

        var (first, last) = SplitName(order.BuyerName);
        string receiverName = string.IsNullOrWhiteSpace(last) ? first : $"{first} {last}";

        var request = new ShiptomoreShipmentRequest
        {
            ProviderSlug = (context.SelectedSubCarrier ?? string.Empty).Trim().ToLowerInvariant(),
            ServiceSlug = (context.ServiceType ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_'),
            ReceiverCountryCode = order.CountryCode.ToUpperInvariant(),
            ReceiverName = receiverName,
            ReceiverPhone = order.Phone,
            ReceiverEmail = order.BuyerEmail,
            ReceiverStreet = order.StreetAddress,
            ReceiverStreet2 = string.IsNullOrWhiteSpace(order.SecondAddress) ? null : order.SecondAddress,
            ReceiverCity = order.City,
            ReceiverZip = order.PostalCode,
            ReceiverStateCode = order.HasState && !string.IsNullOrWhiteSpace(order.State) ? order.State : null,

            // Etsy IOSS numarası: alıcı ID'si değil, SATICI'nın IOSS kaydıdır.
            CollectIdNumber = string.IsNullOrWhiteSpace(order.IossNumber) ? null : order.IossNumber,
            CollectIdType = "ioss",

            ShipmentType = "sale",
            PackageType = "custom",
            CustomerReference = order.OrderNumber,

            IncludeLabels = true,
            LabelFormat = "thermal"
        };

        request.Parcels.Add(new ShiptomoreParcel
        {
            Weight = weight,
            Height = height,
            Width = width,
            Length = length,
            Qty = 1
        });

        foreach (var item in order.Items)
        {
            request.ProductLines.Add(new ShiptomoreProductLine
            {
                Description = string.IsNullOrWhiteSpace(item.Title) ? "E-Ticaret Ürünü" : item.Title,
                Qty = item.Quantity > 0 ? item.Quantity : 1,
                UnitPrice = item.Price,
                OriginCountryCode = "TR",
                HsCode = string.IsNullOrWhiteSpace(context.HsCode) ? null : context.HsCode.Trim()
            });
        }

        if (request.ProductLines.Count == 0)
        {
            request.ProductLines.Add(new ShiptomoreProductLine
            {
                Description = "E-Ticaret Ürünü",
                Qty = 1,
                UnitPrice = order.TotalPrice,
                OriginCountryCode = "TR",
                HsCode = string.IsNullOrWhiteSpace(context.HsCode) ? null : context.HsCode.Trim()
            });
        }

        return request;
    }

    private static (string First, string Last) SplitName(string? fullName)
    {
        string name = (fullName ?? string.Empty).Trim();
        int idx = name.LastIndexOf(' ');
        return idx > 0
            ? (name[..idx], name[(idx + 1)..])
            : (name, string.Empty);
    }

    private static ShipmentCreationResult Fail(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
