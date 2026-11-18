using EtsyMarketPlace.Domain.Subscriptions.Entities;

namespace EtsyMarketPlace.Application.Subscriptions;

public sealed record PaymentGatewaySessionCreateInput(
    string UserId,
    string Username,
    string UserEmail,
    SubscriptionPlan Plan,
    string BillingCycle,
    decimal AmountUsd,
    decimal AmountTry,
    string Currency,
    string InvoiceNumber,
    string? ReturnBaseUrl,
    string? ClientIp = null);

public sealed record PaymentGatewaySessionCreateResult(
    bool Success,
    string SessionId,
    string CheckoutUrl,
    string Mode,
    string Provider,
    DateTime ExpiresAtUtc,
    string? ErrorMessage = null);

public sealed record VirtualPosChargeResult(
    bool Success,
    string CardMaskedNumber,
    string ProviderReference,
    string? ErrorMessage = null);

public sealed record PaymentCallbackVerifyResult(
    bool IsSignatureValid,
    bool IsPaid,
    string SessionId,
    string Provider,
    string? CardMaskedNumber = null,
    string? CardHolderName = null,
    string? ErrorMessage = null);

/// <summary>
/// İyzico, PayTR, Stripe ve dahili Sanal POS ödeme geçidi portu.
/// </summary>
public interface ISubscriptionPaymentGateway
{
    Task<PaymentGatewaySessionCreateResult> CreateCheckoutSessionAsync(
        PaymentGatewaySessionCreateInput input,
        CancellationToken cancellationToken = default);

    Task<VirtualPosChargeResult> ProcessVirtualPosPaymentAsync(
        SubscriptionTransaction transaction,
        ConfirmVirtualPosPaymentRequest cardRequest,
        CancellationToken cancellationToken = default);

    Task<PaymentCallbackVerifyResult> VerifyPaytrCallbackAsync(
        PaytrCallbackInput input,
        CancellationToken cancellationToken = default);

    Task<PaymentCallbackVerifyResult> VerifyIyzicoCallbackAsync(
        IyzicoCallbackInput input,
        CancellationToken cancellationToken = default);

    Task<PaymentGatewaySettingsDto> GetSettingsSummaryAsync(
        CancellationToken cancellationToken = default);
}
