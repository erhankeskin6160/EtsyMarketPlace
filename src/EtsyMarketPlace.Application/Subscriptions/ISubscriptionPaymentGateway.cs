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
    string? ReturnBaseUrl);

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

/// <summary>
/// Stripe ve dahili Stripe Sanal POS ödeme geçidi portu.
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
}
