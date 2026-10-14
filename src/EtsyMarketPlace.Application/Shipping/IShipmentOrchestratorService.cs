namespace EtsyMarketPlace.Application.Shipping;

using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;

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
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed class ConnectCarrierResult
{
    public bool Success { get; set; }
    public string CarrierKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string? TokenSnippet { get; set; }
}

public interface IShipmentOrchestratorService
{
    Task<CreateCarrierShipmentResult> CreateShipmentAsync(CreateCarrierShipmentCommand command, CancellationToken cancellationToken = default);
    Task<ConnectCarrierResult> ConnectCarrierAsync(ConnectCarrierCommand command, CancellationToken cancellationToken = default);
}
