namespace EtsyMarketPlace.Domain.Shipping;

using System.Text.Json.Serialization;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// ShipEntegra canlı fiyat hesaplama isteği modeli.
/// </summary>
public sealed class ShipEntegraQuoteRequest
{
    public string SenderCountry { get; set; } = "TR";
    public string ReceiverCountry { get; set; } = "US";
    public string ReceiverPostalCode { get; set; } = "8537";

    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;

    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gümrük tarife hesaplaması için 10 haneli GTİP / HS kodu (örn. 3926400000).
    /// </summary>
    public string HsCode { get; set; } = "3926400000";

    /// <summary>
    /// Gümrük beyan değeri ($ USD).
    /// </summary>
    public decimal DeclaredValueUsd { get; set; } = 1.00m;

    /// <summary>
    /// Beyan edilen ürün adedi.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Hacimsel Desi hesabı: (En x Boy x Yükseklik) / 5000
    /// </summary>
    public double CalculatedDesi => ComputeDesi(WidthCm, LengthCm, HeightCm);

    /// <summary>
    /// Faturalandırmaya esas ağırlık: max(Gerçek Ağırlık, Desi)
    /// </summary>
    public double BillableWeightKg => ComputeBillableWeight(WeightKg, WidthCm, LengthCm, HeightCm);

    public static double ComputeDesi(double widthCm, double lengthCm, double heightCm) =>
        Math.Round((widthCm * lengthCm * heightCm) / 5000.0, 2);

    public static double ComputeBillableWeight(double weightKg, double widthCm, double lengthCm, double heightCm) =>
        Math.Max(weightKg, ComputeDesi(widthCm, lengthCm, heightCm));
}

/// <summary>
/// ShipEntegra API'sinden veya yerel tarife listesinden dönen kargo teklifi.
/// </summary>
public sealed class ShipEntegraQuoteOffer
{
    public string ServiceName { get; set; } = string.Empty; // "shipentegra-amerika-eko-plus"
    public string ClearServiceName { get; set; } = string.Empty; // "ShipEntegra Amerika Eko Plus"
    public string ServiceType { get; set; } = "ECO"; // "ECO" veya "EXPRESS"
    public decimal CargoPrice { get; set; }
    public decimal FuelCost { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string AdditionalDescription { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
    public bool IsBestCarrier { get; set; }
    public bool IsLivePrice { get; set; } = true;

    // --- NAVLUN KIRILIMI ---
    public decimal FuelMultiplierPercent { get; set; }
    public decimal AdditionalFeeUsd { get; set; }
    public string AdditionalFeeDescription { get; set; } = string.Empty;
    public decimal DiscountedTotalPriceUsd { get; set; }
    public string PricingTemplate { get; set; } = string.Empty;
    public string ReturnPolicyNote { get; set; } = string.Empty;
    public string InsuranceNote { get; set; } = string.Empty;

    // --- HIZLI GÜMRÜK ÖDEMESİ (DDP / TARİFE KIRILIMI) ---
    public decimal DeclaredValueUsd { get; set; } = 1.00m;
    public string HsCode { get; set; } = "3926400000";
    public decimal GeneralDutyUsd { get; set; }
    public decimal GeneralDutyRatePercent { get; set; }
    public decimal AdditionalDutyUsd { get; set; }
    public decimal AdditionalDutyRatePercent { get; set; }
    public string AdditionalDutyHtsCode { get; set; } = string.Empty;
    public decimal TaxAndDutySubtotalUsd { get; set; }
    public decimal CarrierProcessingFeeUsd { get; set; }
    public decimal FastCustomsPaymentUsd { get; set; }
    public decimal GrandTotalUsd { get; set; }
    public decimal DiscountedGrandTotalUsd { get; set; }

    /// <summary>
    /// AdditionalDescription içerisinden "3-6 iş günü" veya benzeri teslimat süresini çıkarır.
    /// </summary>
    public string DeliveryDaysText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AdditionalDescription)) return "Belirtilmedi";
            var match = Regex.Match(AdditionalDescription, @"Tahmini Teslim Süresi\s*([0-9\-]+\s*iş günü)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            return AdditionalDescription.Replace("<br>", " ").Trim();
        }
    }
}

/// <summary>
/// ShipEntegra kullanıcı ayarları ve oturum tokeni.
/// </summary>
public sealed class ShipEntegraSettings
{
    public string BearerToken { get; set; } = string.Empty;
    public DateTime? TokenLastUpdatedUtc { get; set; }
    public double DefaultWeightKg { get; set; } = 0.4;
    public double DefaultWidthCm { get; set; } = 15.0;
    public double DefaultLengthCm { get; set; } = 20.0;
    public double DefaultHeightCm { get; set; } = 10.0;
    public string DefaultCountry { get; set; } = "US";
    public string DefaultPostalCode { get; set; } = "8537";

    // Otomatik Oturum & Token Yenileme (Kimlik Bilgileri)
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string SavedEmail { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? RefreshTokenLastUpdatedUtc { get; set; }
    public bool AutoRefreshEnabled { get; set; } = true;

    [JsonIgnore]

    public string CleanToken
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BearerToken)) return string.Empty;
            string t = BearerToken.Trim();
            if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring(7).Trim();
            }
            return t;
        }
    }

    [JsonIgnore]

    public bool HasValidTokenFormat
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BearerToken) || CleanToken.Length <= 20) return false;
            var exp = JwtTokenInspector.GetExpirationUtc(CleanToken);
            if (exp.HasValue)
            {
                return DateTime.UtcNow < exp.Value;
            }
            return true;
        }
    }
}

/// <summary>
/// ShipEntegra sipariş oluşturma isteği (panel sözleşmesi, gerçek trafikten birebir alındı).
/// </summary>
public sealed class ShipEntegraCreateOrderRequest
{
    [JsonPropertyName("rememberSenderAddress")] public bool RememberSenderAddress { get; set; }
    [JsonPropertyName("shipTo")] public ShipEntegraShipTo ShipTo { get; set; } = new();
    [JsonPropertyName("rememberShipToContact")] public bool RememberShipToContact { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("reference")] public string Reference { get; set; } = string.Empty;
    /// <summary>
    /// Gümrük/gönderim tipi: 1=DDP, 2=DDU, 3=IOSS, 4=ShipEntegra IOSS, 5=HMRC, 7=VOEC.
    /// Panel manuel sipariş akışı alanı boş bırakır (istekte yer almaz). ABD için DDP(1)
    /// canlıda doğrulandı; GB/UK için sunucu DDP'yi reddeder (29.09.2026 canlı).
    /// </summary>
    [JsonPropertyName("shippingType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ShippingType { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("products")] public List<ShipEntegraOrderProduct> Products { get; set; } = new();
    [JsonPropertyName("packages")] public List<ShipEntegraOrderPackage> Packages { get; set; } = new();
}

/// <summary>ShipEntegra alıcı adresi (boş opsiyonel alanlar istekte yer almaz).</summary>
public sealed class ShipEntegraShipTo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("address1")] public string Address1 { get; set; } = string.Empty;

    [JsonPropertyName("address2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Address2 { get; set; }

    [JsonPropertyName("city")] public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? State { get; set; }

    [JsonPropertyName("zipCode")] public string ZipCode { get; set; } = string.Empty;
    [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }
}

/// <summary>ShipEntegra sipariş kalemi.</summary>
public sealed class ShipEntegraOrderProduct
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("quantity")] public int Quantity { get; set; } = 1;
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("hsCode")] public string HsCode { get; set; } = string.Empty;
}

/// <summary>ShipEntegra koli bilgisi (cm / kg).</summary>
public sealed class ShipEntegraOrderPackage
{
    [JsonPropertyName("packageQuantity")] public int PackageQuantity { get; set; } = 1;
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
    [JsonPropertyName("length")] public double Length { get; set; }
    [JsonPropertyName("height")] public double Height { get; set; }
}

/// <summary>ShipEntegra sipariş oluşturma sonucu (sipariş ve kalem kimlikleri).</summary>
public sealed class ShipEntegraOrderResult
{
    public long OrderId { get; set; }
    public List<long> ItemIds { get; set; } = new();
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>
/// ShipEntegra etiket oluşturma isteği (panel sözleşmesi, gerçek trafikten birebir alındı).
/// </summary>
public sealed class ShipEntegraCreateLabelRequest
{
    [JsonPropertyName("specialService")] public string SpecialService { get; set; } = "shipentegra-express";
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("iossNumber")] public string IossNumber { get; set; } = string.Empty;
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    [JsonPropertyName("items")] public List<ShipEntegraLabelItem> Items { get; set; } = new();
    [JsonPropertyName("orderId")] public long OrderId { get; set; }
    [JsonPropertyName("serviceType")] public int ServiceType { get; set; } = 1;
    [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;
    [JsonPropertyName("insurance")] public bool Insurance { get; set; }
    [JsonPropertyName("noTracking")] public bool NoTracking { get; set; }
    [JsonPropertyName("verpackg")] public int Verpackg { get; set; } = -1;
}

/// <summary>Etiket isteğindeki tek kalem.</summary>
public sealed class ShipEntegraLabelItem
{
    [JsonPropertyName("itemId")] public long ItemId { get; set; }
    [JsonPropertyName("declaredPrice")] public decimal DeclaredPrice { get; set; }
    [JsonPropertyName("declaredQuantity")] public int DeclaredQuantity { get; set; } = 1;
    [JsonPropertyName("orderId")] public long OrderId { get; set; }
    [JsonPropertyName("errorMessage")] public Dictionary<string, object> ErrorMessage { get; set; } = new();
    [JsonPropertyName("gtip")] public string Gtip { get; set; } = string.Empty;
}

/// <summary>
/// ShipEntegra oturum tokeninin süresi dolduğunda veya yetki reddedildiğinde fırlatılan özel istisna.
/// </summary>
public sealed class ShipEntegraTokenExpiredException : Exception
{
    public ShipEntegraTokenExpiredException(string message) : base(message) { }
    public ShipEntegraTokenExpiredException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// ShipEntegra iş kuralı reddi (örn. ERR.28050.1008: hedef ülke için DDP geçerli değil).
/// Yetki hatası değildir; oturum yenileme akışını tetiklememesi gerekir.
/// </summary>
public sealed class ShipEntegraBusinessException : Exception
{
    public ShipEntegraBusinessException(string code, string description, int statusCode)
        : base(string.IsNullOrWhiteSpace(description) ? code : $"{description} ({code})")
    {
        Code = code ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? Code : description;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public string Description { get; }

    public int StatusCode { get; }
}

/// <summary>
/// ShipEntegra oturum giriş yanıtındaki token çifti (v4.public biçimi).
/// </summary>
public sealed class ShipEntegraAuthTokens
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// ShipEntegra taşıyıcı bazlı tarife ve gümrük vergisi hesaplama isteği (POST /v1/tools/tariff-calculator/by-carrier).
/// </summary>
public sealed class ShipEntegraTariffRequest
{
    public string Carrier { get; set; } = "shipentegra-amerika-eko-plus";
    public string Currency { get; set; } = "USD";
    public string HtsCode { get; set; } = "3926400000";
    public string Origin { get; set; } = "TR";
    public string Destination { get; set; } = "US";
    public decimal UnitPrice { get; set; } = 1.00m;
    public int Quantity { get; set; } = 1;
    public double Weight { get; set; } = 0.4;
}

/// <summary>
/// Tarife API yanıtındaki tek gümrük vergisi veya taşıyıcı işlem bedeli kalemi.
/// </summary>
public sealed class ShipEntegraDutyItem
{
    public string Type { get; set; } = string.Empty;
    public string HtsCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal AdValRate { get; set; }
}

/// <summary>
/// Taşıyıcı bazlı Hızlı Gümrük Ödemesi (DDP) hesaplama yanıtı.
/// </summary>
public sealed class ShipEntegraTariffResponse
{
    public bool Success { get; set; }
    public decimal GeneralDutyUsd { get; set; }
    public decimal GeneralDutyRatePercent { get; set; }
    public decimal AdditionalDutyUsd { get; set; }
    public decimal AdditionalDutyRatePercent { get; set; }
    public string AdditionalDutyHtsCode { get; set; } = string.Empty;
    public decimal CarrierProcessingFeeUsd { get; set; }
    public decimal TaxAndDutySubtotalUsd { get; set; }
    public decimal FastCustomsPaymentUsd { get; set; }
    public List<ShipEntegraDutyItem> Duties { get; set; } = [];
    public string DiagnosticMessage { get; set; } = string.Empty;
}

/// <summary>
/// ShipEntegra navlun açıklaması ve Hızlı Gümrük Ödemesi hesaplamaları için saf domain yardımcısı.
/// </summary>
public static class ShipEntegraTariffCalculator
{
    public const decimal PrepayDiscountRatio = 23.26m / 23.96m;

    /// <summary>
    /// calculate/all uç noktasından gelen serviceName değerini tariff-calculator/by-carrier slug biçimine normalize eder.
    /// </summary>
    public static string NormalizeTariffCarrierSlug(string? serviceName)
    {
        string raw = (serviceName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(raw)) return "shipentegra-amerika-eko-plus";

        string lower = raw.ToLowerInvariant();
        return lower switch
        {
            "shipentegra" => "shipentegra-express",
            "shipentegra-ups-express" => "shipentegra-ups",
            "shipentegra-eko" or "shipentegra-eko-plus" => "shipentegra-amerika-eko-plus",
            _ => lower
        };
    }

    /// <summary>
    /// Taşıyıcı ve hedef ülkeye göre Hızlı Gümrük Ödemesi taşıyıcı işlem bedelini döndürür.
    /// </summary>
    public static decimal GetDefaultCarrierProcessingFeeUsd(string? serviceName, string? destinationCountry)
    {
        string country = (destinationCountry ?? "US").Trim().ToUpperInvariant();
        if (country != "US")
        {
            // ABD dışı ülkelere DDP hızlı gümrük taşıyıcı bedeli servis tipine göre minimum düzeydedir
            string nonUs = (serviceName ?? string.Empty).Trim().ToLowerInvariant();
            if (nonUs.Contains("fedex")) return 3.00m;
            if (nonUs.Contains("express") || nonUs.Contains("expedited") || nonUs.Contains("ups")) return 4.50m;
            return 0.45m;
        }

        string lower = NormalizeTariffCarrierSlug(serviceName);
        if (lower.Contains("smart-express")) return 0.00m;
        if (lower.Contains("fedex")) return 3.00m;
        if (lower is "shipentegra-express" or "shipentegra-expedited" or "shipentegra-ups" or "shipentegra-ups-ekspress" or "shipentegra-karayolu")
        {
            return 4.50m;
        }
        return 0.45m;
    }

    /// <summary>
    /// Destinasyon ve HS koduna göre varsayılan gümrük vergi oranlarını döndürür (çevrimdışı/yedek).
    /// </summary>
    public static (decimal GeneralRate, decimal AdditionalRate, string AdditionalHtsCode) GetDefaultDutyRates(string? hsCode, string? destinationCountry)
    {
        string country = (destinationCountry ?? "US").Trim().ToUpperInvariant();
        string digits = new string((hsCode ?? string.Empty).Where(char.IsDigit).ToArray());

        if (country == "US")
        {
            // Canlı ShipEntegra US tariff-calculator ile doğrulanan oranlar (HS 3926: %5.3 + %12.5 99030579 = %17.8)
            decimal gen = digits.StartsWith("3926", StringComparison.Ordinal) ? 5.3m
                : digits.StartsWith("9503", StringComparison.Ordinal) ? 0.0m
                : digits.StartsWith("7117", StringComparison.Ordinal) ? 11.0m
                : digits.StartsWith("6109", StringComparison.Ordinal) ? 16.5m
                : digits.StartsWith("6912", StringComparison.Ordinal) ? 9.8m
                : digits.StartsWith("4420", StringComparison.Ordinal) ? 3.2m
                : 5.3m;

            return (gen, 12.5m, "99030579");
        }

        return (0.0m, 0.0m, string.Empty);
    }

    /// <summary>
    /// Avantajlı Hemen Öde indirimli navlun tutarını hesaplar (canlı indirim yoksa ~%2.92 / %4 baz navlun indirimi).
    /// </summary>
    public static decimal ComputeDiscountedFreightUsd(decimal cargoPrice, decimal fuelCost, decimal additionalFee, decimal totalPrice, decimal discountedCargoPrice = 0m)
    {
        if (discountedCargoPrice > 0m && discountedCargoPrice < cargoPrice)
        {
            return Math.Round(discountedCargoPrice + fuelCost + additionalFee, 2, MidpointRounding.AwayFromZero);
        }

        if (totalPrice <= 0m) return 0m;
        if (cargoPrice > 0m && totalPrice >= cargoPrice)
        {
            // Express panelindeki $23.96 -> $23.26 = 17.54 baz navlun üzerinden %4 indirim ($0.70)
            decimal baseDiscount = Math.Round(cargoPrice * 0.04m, 2, MidpointRounding.AwayFromZero);
            return Math.Round(Math.Max(0m, totalPrice - baseDiscount), 2, MidpointRounding.AwayFromZero);
        }

        return Math.Round(totalPrice * PrepayDiscountRatio, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// ShipEntegra additionalDescription HTML alanından iade ve sigorta notlarını ayrıştırır.
    /// </summary>
    public static (string ReturnPolicyNote, string InsuranceNote) ParseAdditionalDescription(string? additionalDescription)
    {
        if (string.IsNullOrWhiteSpace(additionalDescription))
        {
            return (string.Empty, string.Empty);
        }

        string[] parts = Regex.Split(additionalDescription, @"<br\s*/?>", RegexOptions.IgnoreCase);
        var returnNotes = new List<string>();
        var insuranceNotes = new List<string>();

        foreach (string rawPart in parts)
        {
            string clean = Regex.Replace(rawPart, @"<[^>]+>", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clean)) continue;

            if (clean.Contains("iade", StringComparison.OrdinalIgnoreCase))
            {
                returnNotes.Add(clean);
            }
            else if (clean.Contains("sigorta", StringComparison.OrdinalIgnoreCase))
            {
                insuranceNotes.Add(clean);
            }
        }

        return (string.Join(" · ", returnNotes), string.Join(" · ", insuranceNotes));
    }

    /// <summary>
    /// Tek bir ShipEntegra teklifine navlun detayları ve Hızlı Gümrük Ödemesi kırılımını uygular.
    /// </summary>
    public static void PopulateCustomsBreakdown(
        ShipEntegraQuoteOffer offer,
        decimal declaredValueUsd,
        int quantity,
        string? hsCode,
        string? destinationCountry,
        decimal generalDutyRatePercent,
        decimal additionalDutyRatePercent,
        string? additionalDutyHtsCode,
        decimal? overrideGeneralDutyUsd = null,
        decimal? overrideAdditionalDutyUsd = null,
        decimal? overrideCarrierFeeUsd = null)
    {
        decimal unitVal = declaredValueUsd > 0m ? declaredValueUsd : 1.00m;
        int qty = quantity > 0 ? quantity : 1;
        decimal totalDeclared = Math.Round(unitVal * qty, 2, MidpointRounding.AwayFromZero);
        string cleanHs = string.IsNullOrWhiteSpace(hsCode) ? "3926400000" : new string(hsCode.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(cleanHs)) cleanHs = "3926400000";

        decimal genDuty = overrideGeneralDutyUsd ?? Math.Round(totalDeclared * (generalDutyRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        decimal addDuty = overrideAdditionalDutyUsd ?? Math.Round(totalDeclared * (additionalDutyRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        decimal taxSubtotal = Math.Round(genDuty + addDuty, 2, MidpointRounding.AwayFromZero);
        decimal carrierFee = overrideCarrierFeeUsd ?? GetDefaultCarrierProcessingFeeUsd(offer.ServiceName, destinationCountry);
        decimal fastCustoms = Math.Round(taxSubtotal + carrierFee, 2, MidpointRounding.AwayFromZero);

        if (offer.DiscountedTotalPriceUsd <= 0m)
        {
            offer.DiscountedTotalPriceUsd = ComputeDiscountedFreightUsd(offer.CargoPrice, offer.FuelCost, offer.AdditionalFeeUsd, offer.TotalPrice);
        }

        offer.DeclaredValueUsd = totalDeclared;
        offer.HsCode = cleanHs;
        offer.GeneralDutyUsd = genDuty;
        offer.GeneralDutyRatePercent = generalDutyRatePercent;
        offer.AdditionalDutyUsd = addDuty;
        offer.AdditionalDutyRatePercent = additionalDutyRatePercent;
        offer.AdditionalDutyHtsCode = additionalDutyHtsCode ?? string.Empty;
        offer.TaxAndDutySubtotalUsd = taxSubtotal;
        offer.CarrierProcessingFeeUsd = carrierFee;
        offer.FastCustomsPaymentUsd = fastCustoms;
        offer.GrandTotalUsd = Math.Round(offer.TotalPrice + fastCustoms, 2, MidpointRounding.AwayFromZero);
        offer.DiscountedGrandTotalUsd = Math.Round(offer.DiscountedTotalPriceUsd + fastCustoms, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Çevrimdışı veya token eksikliği durumunda deterministic yedek tarife hesaplaması döndürür.
    /// </summary>
    public static ShipEntegraTariffResponse ComputeFallbackTariff(ShipEntegraTariffRequest request)
    {
        decimal unitVal = request.UnitPrice > 0m ? request.UnitPrice : 1.00m;
        int qty = request.Quantity > 0 ? request.Quantity : 1;
        decimal totalDeclared = Math.Round(unitVal * qty, 2, MidpointRounding.AwayFromZero);

        var (genRate, addRate, addHts) = GetDefaultDutyRates(request.HtsCode, request.Destination);
        decimal genDuty = Math.Round(totalDeclared * (genRate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal addDuty = Math.Round(totalDeclared * (addRate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal carrierFee = GetDefaultCarrierProcessingFeeUsd(request.Carrier, request.Destination);
        decimal taxSub = Math.Round(genDuty + addDuty, 2, MidpointRounding.AwayFromZero);
        decimal fastCustoms = Math.Round(taxSub + carrierFee, 2, MidpointRounding.AwayFromZero);

        return new ShipEntegraTariffResponse
        {
            Success = true,
            GeneralDutyUsd = genDuty,
            GeneralDutyRatePercent = genRate,
            AdditionalDutyUsd = addDuty,
            AdditionalDutyRatePercent = addRate,
            AdditionalDutyHtsCode = addHts,
            TaxAndDutySubtotalUsd = taxSub,
            CarrierProcessingFeeUsd = carrierFee,
            FastCustomsPaymentUsd = fastCustoms,
            Duties =
            [
                new ShipEntegraDutyItem { Type = "general_duty", HtsCode = request.HtsCode, AdValRate = genRate, Price = genDuty },
                new ShipEntegraDutyItem { Type = "additional_duty", HtsCode = addHts, AdValRate = addRate, Price = addDuty },
                new ShipEntegraDutyItem { Type = "carrier_fee", Price = carrierFee }
            ],
            DiagnosticMessage = "Yerel tarife oranlarıyla hesaplandı."
        };
    }
}
