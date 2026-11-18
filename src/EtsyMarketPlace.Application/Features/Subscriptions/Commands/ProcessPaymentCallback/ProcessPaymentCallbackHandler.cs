using System.Net;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ProcessPaymentCallback;

public sealed class ProcessPaymentCallbackHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    ISubscriptionPaymentGateway paymentGateway,
    ISubscriptionEntitlementService entitlementService,
    JwtTokenService jwtTokenService)
    : IRequestHandler<ProcessPaymentCallbackCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ProcessPaymentCallbackCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        PaymentCallbackVerifyResult verifyResult;
        if (string.Equals(request.Provider, "PayTR", StringComparison.OrdinalIgnoreCase) && request.PaytrInput is not null)
        {
            verifyResult = await paymentGateway.VerifyPaytrCallbackAsync(request.PaytrInput, cancellationToken);
        }
        else if (string.Equals(request.Provider, "Iyzico", StringComparison.OrdinalIgnoreCase) && request.IyzicoInput is not null)
        {
            verifyResult = await paymentGateway.VerifyIyzicoCallbackAsync(request.IyzicoInput, cancellationToken);
        }
        else
        {
            return ServiceResponse.CreateError("Desteklenmeyen ödeme callback sağlayıcısı.", HttpStatusCode.BadRequest);
        }

        if (!verifyResult.IsSignatureValid)
        {
            return ServiceResponse.CreateError(
                verifyResult.ErrorMessage ?? "Ödeme geri bildirim imzası doğrulanamadı.",
                HttpStatusCode.BadRequest);
        }

        var tx = await subscriptionRepository.GetTransactionBySessionIdAsync(verifyResult.SessionId, cancellationToken);
        if (tx is null)
        {
            return ServiceResponse.CreateNotFound($"Ödeme oturumu bulunamadı: {verifyResult.SessionId}");
        }

        if (string.Equals(tx.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResponse.CreateSuccess(
                "Ödeme zaten onaylanmış.",
                new PaymentCallbackProcessResultDto(true, true, tx.SessionId, tx.PaymentProvider, "Paid", "Ödeme zaten onaylanmış."));
        }

        if (!verifyResult.IsPaid)
        {
            tx.Status = "Failed";
            await subscriptionRepository.UpdateTransactionAsync(tx, cancellationToken);
            return ServiceResponse.CreateSuccess(
                verifyResult.ErrorMessage ?? "Ödeme başarısız oldu.",
                new PaymentCallbackProcessResultDto(
                    true,
                    false,
                    tx.SessionId,
                    verifyResult.Provider,
                    "Failed",
                    verifyResult.ErrorMessage ?? "Ödeme banka tarafından onaylanmadı."));
        }

        var masked = string.IsNullOrWhiteSpace(verifyResult.CardMaskedNumber)
            ? $"{verifyResult.Provider} 3D Secure"
            : verifyResult.CardMaskedNumber;
        var holder = string.IsNullOrWhiteSpace(verifyResult.CardHolderName)
            ? tx.Username
            : verifyResult.CardHolderName;

        var confirmed = await SubscriptionLifecycleHelper.ActivatePaidTransactionAsync(
            subscriptionRepository,
            userRepository,
            entitlementService,
            jwtTokenService,
            tx,
            masked,
            holder,
            roleHint: null,
            request.RemoteIpAddress,
            $"{verifyResult.Provider} 3D Secure ödemesi doğrulandı: {tx.PlanName} ({tx.BillingCycle}), Fatura: {tx.InvoiceNumber}",
            cancellationToken);

        return ServiceResponse.CreateSuccess(
            confirmed.Message,
            new PaymentCallbackProcessResultDto(true, true, tx.SessionId, verifyResult.Provider, "Paid", confirmed.Message));
    }
}
