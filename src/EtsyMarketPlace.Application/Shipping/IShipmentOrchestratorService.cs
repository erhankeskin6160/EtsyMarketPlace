namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

public sealed class CreateCarrierShipmentCommand
{
    public long ReceiptId { get; set; }
    public string ShopId { get; set; } = string.Empty;
    public string CarrierKey { get; set; } = string.Empty; // "shipentegra", "shiptomore", "aras", "navlungo"
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string SubCarrier { get; set; } = string.Empty;
    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;
    public string HsCode { get; set; } = string.Empty;
    public decimal CargoPrice { get; set; } = 0m;
    public bool NotifyEtsy { get; set; } = true;
    public EtsyOrderFulfillmentItem? RecipientAddress { get; set; }
}

public sealed class CreateCarrierShipmentResult
{
    public bool Success { get; set; }
    public long ReceiptId { get; set; }
    public string CarrierKey { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string CarrierShipmentId { get; set; } = string.Empty;
    public string LabelUrl { get; set; } = string.Empty;
    public string? LabelBase64 { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool EtsyTrackingSubmitted { get; set; }
    public string EtsyTrackingMessage { get; set; } = string.Empty;
}

public sealed class ConnectCarrierCommand
{
    public string CarrierKey { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? BearerToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public bool OpenBrowser { get; set; }
}

public sealed class ConnectCarrierResult
{
    public bool Success { get; set; }
    public string CarrierKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string? TokenSnippet { get; set; }
    public string? BearerToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class CarrierSessionStatusDto
{
    public string CarrierKey { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool HasRefreshToken { get; set; }
    public string? BearerToken { get; set; }
    public string? TokenSnippet { get; set; }
    public string? ApiKey { get; set; }
    public string? SavedEmail { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class CarrierQuotesQuery
{
    public string CountryCode { get; set; } = "US";
    public string PostalCode { get; set; } = "10001";
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double WeightKg { get; set; } = 0.4;
    public double WidthCm { get; set; } = 15.0;
    public double LengthCm { get; set; } = 20.0;
    public double HeightCm { get; set; } = 10.0;
    public string HsCode { get; set; } = "3926400000";
    public int Quantity { get; set; } = 1;
    public decimalUsdValueOrDefault? DeclaredValueUsd { get; set; }
    public decimal UsdTryRate { get; set; } = 36.25m;
}

[JsonConverter(typeof(DecimalUsdValueOrDefaultJsonConverter))]
public readonly record struct decimalUsdValueOrDefault(decimal Value)
{
    public static implicit operator decimalUsdValueOrDefault(decimal value) => new(value);
}

public sealed class DecimalUsdValueOrDefaultJsonConverter : JsonConverter<decimalUsdValueOrDefault>
{
    public override decimalUsdValueOrDefault Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out decimal num))
            return new decimalUsdValueOrDefault(num);

        if (reader.TokenType == JsonTokenType.String && decimal.TryParse(reader.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal strNum))
            return new decimalUsdValueOrDefault(strNum);

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            decimal val = 1.00m;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string? prop = reader.GetString();
                    reader.Read();
                    if (string.Equals(prop, "value", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.Number)
                    {
                        val = reader.GetDecimal();
                    }
                }
            }
            return new decimalUsdValueOrDefault(val);
        }

        return new decimalUsdValueOrDefault(1.00m);
    }

    public override void Write(Utf8JsonWriter writer, decimalUsdValueOrDefault value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

public sealed class UnifiedCarrierQuoteDto
{
    public string CarrierKey { get; set; } = string.Empty; // "aras", "shipentegra", "navlungo", "shiptomore"
    public string CarrierName { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string SubCarrier { get; set; } = string.Empty;
    public string EstimatedDays { get; set; } = string.Empty;
    public decimal PriceUsd { get; set; }
    public decimal PriceTry { get; set; }
    public bool IsLive { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsLowestPrice { get; set; }
    public string QuoteSourceBadge { get; set; } = string.Empty;

    // --- NAVLUN KIRILIMI ---
    public decimal BaseCargoPriceUsd { get; set; }
    public decimal FuelCostUsd { get; set; }
    public decimal FuelMultiplierPercent { get; set; }
    public decimal AdditionalFeeUsd { get; set; }
    public string AdditionalFeeDescription { get; set; } = string.Empty;
    public decimal DiscountedFreightUsd { get; set; }
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
    public decimal FastCustomsPaymentTry { get; set; }
    public decimal GrandTotalUsd { get; set; }
    public decimal GrandTotalTry { get; set; }
    public decimal DiscountedGrandTotalUsd { get; set; }
}

public sealed class CarrierQuotesResponseDto
{
    public double CalculatedDesi { get; set; }
    public double BillableWeightKg { get; set; }
    public int LiveCount { get; set; }
    public int EstimatedCount { get; set; }
    public string? WarningMessage { get; set; }
    public IReadOnlyList<UnifiedCarrierQuoteDto> Quotes { get; set; } = [];
}

public sealed class HsCodeSearchItemDto
{
    public string Code { get; set; } = string.Empty;
    public string FormattedCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsUsValidated { get; set; }
}

public interface IShipmentOrchestratorService
{
    Task<CreateCarrierShipmentResult> CreateShipmentAsync(CreateCarrierShipmentCommand command, CancellationToken cancellationToken = default);
    Task<ConnectCarrierResult> ConnectCarrierAsync(ConnectCarrierCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarrierSessionStatusDto>> GetCarrierSessionsAsync(CancellationToken cancellationToken = default);
    Task<CarrierQuotesResponseDto> GetCarrierQuotesAsync(CarrierQuotesQuery query, CancellationToken cancellationToken = default);
    IReadOnlyList<HsCodeSearchItemDto> SearchHsCodes(string? query, int limit = 25);
}
