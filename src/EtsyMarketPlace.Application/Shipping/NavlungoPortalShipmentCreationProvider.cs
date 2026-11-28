namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Navlungo üye portalı (api.navlungo.com) üzerinden gönderi oluşturan modüler sağlayıcı.
/// </summary>
public sealed class NavlungoPortalShipmentCreationProvider : IShipmentCreationProvider
{
    private readonly INavlungoApiClient _apiClient;
    private readonly Func<NavlungoSettings> _loadSettings;

    public NavlungoPortalShipmentCreationProvider(
        INavlungoApiClient apiClient,
        Func<NavlungoSettings>? loadSettings = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _loadSettings = loadSettings ?? (() => NavlungoSettingsStore.Load());
    }

    public string ProviderName => "Navlungo";

    public bool IsCreationSupported => true;

    public async Task<ShipmentCreationResult> CreateShipmentAsync(
        ShipmentCreationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context == null || context.Order == null)
        {
            return Fail("Sipariş bilgisi bulunamadı.");
        }

        var settings = _loadSettings() ?? new NavlungoSettings();
        if (!settings.IsSessionValid)
        {
            return Fail("Navlungo oturumu bağlı değil veya süresi dolmuş. Lütfen üst menüden 'Navlungo → Oturum Aç' ile giriş yapıp tekrar deneyin.");
        }

        var order = context.Order;
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(order.BuyerName)) missing.Add("Alıcı Adı Soyadı");
        if (string.IsNullOrWhiteSpace(order.StreetAddress)) missing.Add("Adres Satırı");
        if (string.IsNullOrWhiteSpace(order.City)) missing.Add("Şehir");
        if (string.IsNullOrWhiteSpace(order.PostalCode)) missing.Add("Posta Kodu");

        string email = order.BuyerEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            if (context.NotifyEtsy)
                missing.Add("Alıcı E-postası");
            else
                email = $"order.{order.ReceiptId}@etsy-buyer.com";
        }

        string phone = order.Phone?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
        {
            if (context.NotifyEtsy)
                missing.Add("Alıcı Telefonu");
            else
                phone = "+14805550199";
        }
        else if (!context.NotifyEtsy && new string(phone.Where(char.IsDigit).ToArray()) is "15551234567" or "5551234567")
        {
            // Navlungo ABD telefon doğrulaması 555 alan kodunu reddeder; test modunda geçerli ABD formatına çevir.
            phone = "+14805550199";
        }

        if (missing.Count > 0)
        {
            return Fail("Navlungo için zorunlu alanlar eksik: " + string.Join(", ", missing) +
                        ". Lütfen 'Adresi Düzenle' düğmesinden bu bilgileri tamamlayın.");
        }

        string countryCode = string.IsNullOrWhiteSpace(order.CountryCode)
            ? "US"
            : order.CountryCode.Trim().ToUpperInvariant();
        string stateCode = order.State?.Trim().ToUpperInvariant() ?? string.Empty;

        if ((countryCode == "US" || countryCode == "CA") && string.IsNullOrWhiteSpace(stateCode))
        {
            return Fail($"Navlungo: {countryCode} ülkesi için alıcı eyalet (State) kodu zorunludur (ör. AZ, NY, CA).");
        }

        var request = BuildRequest(context, email, phone, countryCode, stateCode);
        var response = await _apiClient.CreateShipmentAsync(request, settings, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccess)
        {
            return Fail(string.IsNullOrWhiteSpace(response.ErrorMessage)
                ? "Navlungo gönderisi oluşturulamadı."
                : response.ErrorMessage);
        }

        return new ShipmentCreationResult
        {
            IsSuccess = true,
            ShipmentId = response.ShipmentId,
            TrackingNumber = response.TrackingNumber,
            LabelUrl = response.LabelUrl,
            LabelBase64 = response.LabelBase64
        };
    }

    public static NavlungoCreateShipmentRequest BuildRequest(
        ShipmentCreationContext context,
        string email,
        string phone,
        string countryCode,
        string stateCode)
    {
        var order = context.Order;
        int qty = order.Items != null && order.Items.Count > 0
            ? Math.Max(1, order.Items.Sum(i => Math.Max(1, i.Quantity)))
            : 1;
        decimal unitPrice = order.TotalPrice > 0m
            ? Math.Round(order.TotalPrice / qty, 2, MidpointRounding.AwayFromZero)
            : 10.00m;

        string hsDigits = new string((context.HsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(hsDigits))
        {
            hsDigits = "39264000";
        }
        else if (hsDigits.Length > 10)
        {
            hsDigits = hsDigits[..10];
        }

        string rawTitle = order.Items?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Title))?.Title?.Trim()
            ?? context.Category
            ?? string.Empty;
        string productDescription = NormalizeProductDescription(rawTitle);

        return new NavlungoCreateShipmentRequest
        {
            FromCountryCode = "TR",
            ReceiverName = order.BuyerName.Trim(),
            ReceiverEmail = email,
            ReceiverPhone = phone,
            ReceiverStreet1 = order.StreetAddress.Trim(),
            ReceiverStreet2 = order.SecondAddress?.Trim() ?? string.Empty,
            ReceiverCity = order.City.Trim(),
            ReceiverStateCode = stateCode,
            ReceiverPostalCode = order.PostalCode.Trim(),
            ReceiverCountryCode = countryCode,
            WeightKg = context.WeightKg > 0 ? context.WeightKg : 0.4,
            LengthCm = context.LengthCm > 0 ? context.LengthCm : 20.0,
            WidthCm = context.WidthCm > 0 ? context.WidthCm : 15.0,
            HeightCm = context.HeightCm > 0 ? context.HeightCm : 10.0,
            HsCode = hsDigits,
            ProductDescription = productDescription,
            Quantity = qty,
            UnitPriceUsd = unitPrice,
            IossNumber = order.IossNumber?.Trim() ?? string.Empty,
            SelectedSubCarrier = string.IsNullOrWhiteSpace(context.SelectedSubCarrier) ? "widect" : context.SelectedSubCarrier.Trim(),
            SelectedServiceCode = context.SelectedServiceCode?.Trim() ?? string.Empty,
            ServiceType = context.ServiceType?.Trim() ?? "Widect — Navlungo",
            IsDdp = context.IsDdp,
            CustomerReference = order.ReceiptId > 0 ? order.ReceiptId.ToString() : string.Empty
        };
    }

    /// <summary>
    /// Navlungo portal kuralı: Ürün açıklaması en az 15 karakter ve en az 3 kelime olmalıdır.
    /// </summary>
    public static string NormalizeProductDescription(string? raw)
    {
        string cleaned = (raw ?? string.Empty).Trim();
        int wordCount = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (cleaned.Length >= 15 && wordCount >= 3)
        {
            return cleaned.Length <= 120 ? cleaned : cleaned[..120].Trim();
        }

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Decorative Plastic Figurine Item";
        }

        string padded = $"{cleaned} Handmade Decorative Item";
        return padded.Length >= 15 ? padded : $"{padded} Product";
    }

    private static ShipmentCreationResult Fail(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
