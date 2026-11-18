using System.Net;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmAdminTestPayment;

public sealed class ConfirmAdminTestPaymentHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    ISubscriptionEntitlementService entitlementService,
    JwtTokenService jwtTokenService)
    : IRequestHandler<ConfirmAdminTestPaymentCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(ConfirmAdminTestPaymentCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        if (!SubscriptionLifecycleHelper.IsAdminRole(request.RoleHint))
            return ServiceResponse.CreateError("Yönetici test onayı yalnızca Admin kullanıcılar tarafından yapılabilir.", HttpStatusCode.Forbidden);

        if (string.IsNullOrWhiteSpace(request.SessionId))
            return ServiceResponse.CreateError("Ödeme oturum kimliği (sessionId) gereklidir.", HttpStatusCode.BadRequest);

        var tx = await subscriptionRepository.GetTransactionBySessionIdAsync(request.SessionId.Trim(), cancellationToken);
        if (tx is null)
            return ServiceResponse.CreateNotFound("Sanal POS ödeme oturumu bulunamadı.");

        if (string.Equals(tx.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            var existingSummary = await entitlementService.GetEntitlementSummaryAsync(
                tx.UserId, request.RoleHint, tx.Username, cancellationToken);
            return ServiceResponse.CreateSuccess(
                "Bu abonelik ödemesi zaten onaylanmış.",
                new ConfirmPaymentResponseDto(true, "Ödeme zaten onaylanmış.", tx.ToDto(), existingSummary));
        }

        var confirmed = await SubscriptionLifecycleHelper.ActivatePaidTransactionAsync(
            subscriptionRepository,
            userRepository,
            entitlementService,
            jwtTokenService,
            tx,
            cardMaskedNumber: "Yönetici Test Onayı",
            cardHolderName: request.UsernameHint ?? "Admin",
            request.RoleHint,
            request.RemoteIpAddress,
            $"Yönetici Test Onayı ile paket aktifleştirildi: {tx.PlanName} ({tx.BillingCycle}), Fatura: {tx.InvoiceNumber}",
            cancellationToken);

        return ServiceResponse.CreateSuccess(confirmed.Message, confirmed);
    }
}
