using System.Net;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmCheckoutPayment;

public sealed class ConfirmCheckoutPaymentHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    ISubscriptionPaymentGateway paymentGateway,
    ISubscriptionEntitlementService entitlementService,
    JwtTokenService jwtTokenService)
    : IRequestHandler<ConfirmCheckoutPaymentCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ConfirmCheckoutPaymentCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var tx = await subscriptionRepository.GetTransactionBySessionIdAsync(
            request.Request.SessionId.Trim(),
            cancellationToken);

        if (tx is null)
        {
            return ServiceResponse.CreateNotFound("Sanal POS ödeme oturumu bulunamadı.");
        }

        var isAdmin = SubscriptionLifecycleHelper.IsAdminRole(request.RoleHint);
        if (!string.Equals(tx.UserId, request.UserId, StringComparison.Ordinal) && !isAdmin)
        {
            return ServiceResponse.CreateError("Bu ödeme oturumunu onaylama yetkiniz yok.", HttpStatusCode.Forbidden);
        }

        if (string.Equals(tx.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            var existingSummary = await entitlementService.GetEntitlementSummaryAsync(
                tx.UserId, request.RoleHint, tx.Username, cancellationToken);
            return ServiceResponse.CreateSuccess(
                "Bu abonelik ödemesi zaten onaylanmış.",
                new ConfirmPaymentResponseDto(true, "Ödeme zaten onaylanmış.", tx.ToDto(), existingSummary));
        }

        var chargeResult = await paymentGateway.ProcessVirtualPosPaymentAsync(tx, request.Request, cancellationToken);
        if (!chargeResult.Success)
        {
            tx.Status = "Failed";
            await subscriptionRepository.UpdateTransactionAsync(tx, cancellationToken);
            return ServiceResponse.CreateError(
                chargeResult.ErrorMessage ?? "Sanal POS kart provizyonu reddedildi.",
                HttpStatusCode.PaymentRequired);
        }

        var confirmed = await SubscriptionLifecycleHelper.ActivatePaidTransactionAsync(
            subscriptionRepository,
            userRepository,
            entitlementService,
            jwtTokenService,
            tx,
            chargeResult.CardMaskedNumber,
            request.Request.CardHolderName.Trim(),
            request.RoleHint,
            request.RemoteIpAddress,
            $"Sanal POS ödemesi onaylandı ({tx.PaymentProvider}): {tx.PlanName} ({tx.BillingCycle}), Fatura: {tx.InvoiceNumber}, Kart: {chargeResult.CardMaskedNumber}",
            cancellationToken);

        return ServiceResponse.CreateSuccess(confirmed.Message, confirmed);
    }
}
