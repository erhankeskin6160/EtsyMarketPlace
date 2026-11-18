namespace EtsyMarketPlace.Domain.Subscriptions.Entities;

/// <summary>
/// Admin panelinden veya yapılandırmadan yönetilen Sanal POS / ödeme geçidi ayarları.
/// </summary>
public class PaymentGatewayConfig
{
    public string Id { get; set; } = "default";
    public string ActiveProvider { get; set; } = "Unconfigured";
    public bool IsSandbox { get; set; } = true;

    public string IyzicoApiKey { get; set; } = string.Empty;
    public string IyzicoSecretKey { get; set; } = string.Empty;
    public string IyzicoBaseUrl { get; set; } = "https://sandbox-api.iyzipay.com";

    public string PaytrMerchantId { get; set; } = string.Empty;
    public string PaytrMerchantKey { get; set; } = string.Empty;
    public string PaytrMerchantSalt { get; set; } = string.Empty;

    public string StripeSecretKey { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
