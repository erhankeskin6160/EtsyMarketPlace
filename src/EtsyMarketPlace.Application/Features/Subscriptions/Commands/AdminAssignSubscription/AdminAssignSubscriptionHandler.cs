using System.Net;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminAssignSubscription;

public sealed class AdminAssignSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    ISubscriptionEntitlementService entitlementService)
    : IRequestHandler<AdminAssignSubscriptionCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(AdminAssignSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var targetUser = await SubscriptionLifecycleHelper.TryFindUserAsync(
            userRepository,
            request.TargetUserId,
            cancellationToken);

        if (targetUser is null)
        {
            return ServiceResponse.CreateNotFound("Kullanıcı bulunamadı.");
        }

        var plan = await SubscriptionLifecycleHelper.ResolvePlanAsync(
            subscriptionRepository,
            request.Request.PlanId,
            cancellationToken);

        if (plan is null)
        {
            return ServiceResponse.CreateNotFound("Abonelik paketi bulunamadı.");
        }

        var now = DateTime.UtcNow;
        var billingCycle = SubscriptionLifecycleHelper.NormalizeBillingCycle(request.Request.BillingCycle);
        var status = string.IsNullOrWhiteSpace(request.Request.Status)
            ? SubscriptionStatus.Active.ToString()
            : Enum.TryParse<SubscriptionStatus>(request.Request.Status.Trim(), ignoreCase: true, out var parsed)
                ? parsed.ToString()
                : SubscriptionStatus.Active.ToString();

        var periodEnd = SubscriptionLifecycleHelper.CalculatePeriodEnd(now, billingCycle, request.Request.DurationDays);
        var effectiveQuota = request.Request.CustomAiTokenQuotaOverride ?? plan.MonthlyAiTokenQuota;

        var existingSub = await subscriptionRepository.GetUserSubscriptionAsync(targetUser.Id, cancellationToken);
        var sub = new UserSubscription
        {
            Id = existingSub?.Id ?? Guid.NewGuid().ToString("N"),
            UserId = targetUser.Id,
            PlanId = plan.Id,
            Status = status,
            BillingCycle = billingCycle,
            StartDateUtc = existingSub?.StartDateUtc ?? now,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = periodEnd,
            AutoRenew = existingSub?.AutoRenew ?? true,
            CustomMaxShopsOverride = request.Request.CustomMaxShopsOverride,
            CustomAiTokenQuotaOverride = request.Request.CustomAiTokenQuotaOverride,
            PaymentProvider = "AdminAssignment",
            LastSessionId = existingSub?.LastSessionId,
            UpdatedAtUtc = now
        };

        await subscriptionRepository.UpsertUserSubscriptionAsync(sub, effectiveQuota, cancellationToken);
        await SubscriptionLifecycleHelper.TryUpdateUserSnapshotAsync(
            userRepository,
            targetUser,
            plan.Id,
            status,
            periodEnd,
            effectiveQuota,
            cancellationToken);

        await SubscriptionLifecycleHelper.TryAddAuditLogAsync(
            userRepository,
            request.AdminId ?? "admin",
            request.AdminName ?? "Admin",
            "AdminAssignSubscription",
            $"Kullanıcı {targetUser.Username} -> {plan.Name} ({status}, {billingCycle}, Kota: {effectiveQuota:N0}) olarak atandı.",
            request.RemoteIpAddress,
            cancellationToken);

        var summary = await entitlementService.GetEntitlementSummaryAsync(
            targetUser.Id,
            targetUser.Role,
            targetUser.Username,
            cancellationToken);

        return ServiceResponse.CreateSuccess(
            $"{targetUser.Username} kullanıcısına {plan.Name} paketi atandı.",
            summary);
    }
}
