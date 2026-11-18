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

        var now = DateTime.UtcNow;
        tx.Status = "Paid";
        tx.PaidAtUtc = now;
        tx.CardMaskedNumber = chargeResult.CardMaskedNumber;
        tx.CardHolderName = request.Request.CardHolderName.Trim();
        tx = await subscriptionRepository.UpdateTransactionAsync(tx, cancellationToken);

        var plan = await SubscriptionLifecycleHelper.ResolvePlanAsync(subscriptionRepository, tx.PlanId, cancellationToken)
                   ?? SubscriptionFeatureKeys.GetDefaultPlans().First(p => p.Id == SubscriptionPlanCode.Pro);

        var periodEnd = SubscriptionLifecycleHelper.CalculatePeriodEnd(now, tx.BillingCycle);
        var existingSub = await subscriptionRepository.GetUserSubscriptionAsync(tx.UserId, cancellationToken);

        var nextSub = existingSub is null
            ? new UserSubscription
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = tx.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active.ToString(),
                BillingCycle = tx.BillingCycle,
                StartDateUtc = now,
                CurrentPeriodStartUtc = now,
                CurrentPeriodEndUtc = periodEnd,
                AutoRenew = true,
                PaymentProvider = tx.PaymentProvider,
                LastSessionId = tx.SessionId,
                UpdatedAtUtc = now
            }
            : new UserSubscription
            {
                Id = existingSub.Id,
                UserId = existingSub.UserId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active.ToString(),
                BillingCycle = tx.BillingCycle,
                StartDateUtc = existingSub.StartDateUtc,
                CurrentPeriodStartUtc = now,
                CurrentPeriodEndUtc = periodEnd,
                AutoRenew = true,
                CustomMaxShopsOverride = null,
                CustomAiTokenQuotaOverride = null,
                PaymentProvider = tx.PaymentProvider,
                LastSessionId = tx.SessionId,
                UpdatedAtUtc = now
            };

        await subscriptionRepository.UpsertUserSubscriptionAsync(nextSub, plan.MonthlyAiTokenQuota, cancellationToken);

        var user = await SubscriptionLifecycleHelper.TryFindUserAsync(userRepository, tx.UserId, cancellationToken);
        await SubscriptionLifecycleHelper.TryUpdateUserSnapshotAsync(
            userRepository,
            user,
            plan.Id,
            SubscriptionStatus.Active.ToString(),
            periodEnd,
            plan.MonthlyAiTokenQuota,
            cancellationToken);

        await SubscriptionLifecycleHelper.TryAddAuditLogAsync(
            userRepository,
            tx.UserId,
            tx.Username,
            "SubscriptionUpgraded",
            $"Stripe Sanal POS ödemesi onaylandı: {plan.Name} ({tx.BillingCycle}), Fatura: {tx.InvoiceNumber}, Kart: {tx.CardMaskedNumber}",
            request.RemoteIpAddress,
            cancellationToken);

        UserDto? updatedUserDto = null;
        string? refreshedToken = null;
        if (user is not null)
        {
            var updatedUser = user with
            {
                SubscriptionPlanId = plan.Id,
                SubscriptionStatus = SubscriptionStatus.Active.ToString(),
                SubscriptionExpiresAt = new DateTimeOffset(periodEnd, TimeSpan.Zero),
                MonthlyAiTokenQuota = plan.MonthlyAiTokenQuota
            };
            updatedUserDto = updatedUser.ToDto();
            refreshedToken = jwtTokenService.GenerateToken(updatedUser);
        }

        var summary = await entitlementService.GetEntitlementSummaryAsync(
            tx.UserId,
            user?.Role ?? request.RoleHint,
            tx.Username,
            cancellationToken);

        return ServiceResponse.CreateSuccess(
            $"{plan.Name} paketi başarıyla aktifleştirildi!",
            new ConfirmPaymentResponseDto(
                true,
                $"{plan.Name} paketi başarıyla aktifleştirildi!",
                tx.ToDto(),
                summary,
                updatedUserDto,
                refreshedToken));
    }
}
