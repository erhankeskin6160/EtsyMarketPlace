using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;

namespace EtsyMarketPlace.Application.Subscriptions;

internal static class SubscriptionLifecycleHelper
{
    internal static bool IsAdminRole(string? role)
        => string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

    internal static async Task<AppUser?> TryFindUserAsync(
        IUserRepository userRepository,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        try
        {
            return await userRepository.GetByIdAsync(userId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    internal static async Task TryUpdateUserSnapshotAsync(
        IUserRepository userRepository,
        AppUser? existingUser,
        string planId,
        string status,
        DateTime expiresAtUtc,
        int monthlyQuota,
        CancellationToken cancellationToken)
    {
        if (existingUser is null) return;
        try
        {
            var updated = existingUser with
            {
                SubscriptionPlanId = planId,
                SubscriptionStatus = status,
                SubscriptionExpiresAt = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
                MonthlyAiTokenQuota = monthlyQuota
            };
            await userRepository.UpdateUserAsync(updated, cancellationToken);
        }
        catch
        {
            // PostgreSQL kullanıcı tablosu ulaşılamadığında abonelik akışı kesilmesin
        }
    }

    internal static async Task TryAddAuditLogAsync(
        IUserRepository userRepository,
        string userId,
        string username,
        string action,
        string details,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            await userRepository.AddAuditLogAsync(
                new AuditLogEntry(0, userId, username, action, details, ipAddress, DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch
        {
            // Audit log yazımı başarısız olsa da ödeme/abonelik aktivasyonu geri alınmaz
        }
    }

    internal static async Task<SubscriptionPlan?> ResolvePlanAsync(
        ISubscriptionRepository subscriptionRepository,
        string planId,
        CancellationToken cancellationToken)
    {
        var normalized = SubscriptionPlanCode.Normalize(planId);
        var plan = await subscriptionRepository.GetPlanByIdAsync(normalized, cancellationToken);
        return plan ?? SubscriptionFeatureKeys.GetDefaultPlans()
            .FirstOrDefault(p => string.Equals(p.Id, normalized, StringComparison.OrdinalIgnoreCase));
    }

    internal static string NormalizeBillingCycle(string? cycle)
        => string.Equals(cycle?.Trim(), BillingCycle.Yearly.ToString(), StringComparison.OrdinalIgnoreCase)
            ? BillingCycle.Yearly.ToString()
            : BillingCycle.Monthly.ToString();

    internal static string NormalizeCurrency(string? currency)
        => string.Equals(currency?.Trim(), "TRY", StringComparison.OrdinalIgnoreCase) ? "TRY" : "USD";

    internal static DateTime CalculatePeriodEnd(DateTime startUtc, string billingCycle, int? durationDays = null)
    {
        if (durationDays.HasValue && durationDays.Value > 0)
            return startUtc.AddDays(durationDays.Value);

        return string.Equals(billingCycle, BillingCycle.Yearly.ToString(), StringComparison.OrdinalIgnoreCase)
            ? startUtc.AddYears(1)
            : startUtc.AddMonths(1);
    }

    internal static async Task<ConfirmPaymentResponseDto> ActivatePaidTransactionAsync(
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        ISubscriptionEntitlementService entitlementService,
        JwtTokenService jwtTokenService,
        SubscriptionTransaction tx,
        string? cardMaskedNumber,
        string? cardHolderName,
        string? roleHint,
        string? remoteIpAddress,
        string auditDetails,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        tx.Status = "Paid";
        tx.PaidAtUtc = now;
        if (!string.IsNullOrWhiteSpace(cardMaskedNumber))
            tx.CardMaskedNumber = cardMaskedNumber;
        if (!string.IsNullOrWhiteSpace(cardHolderName))
            tx.CardHolderName = cardHolderName.Trim();

        tx = await subscriptionRepository.UpdateTransactionAsync(tx, cancellationToken);

        var plan = await ResolvePlanAsync(subscriptionRepository, tx.PlanId, cancellationToken)
                   ?? SubscriptionFeatureKeys.GetDefaultPlans().First(p => p.Id == SubscriptionPlanCode.Pro);

        var periodEnd = CalculatePeriodEnd(now, tx.BillingCycle);
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

        var user = await TryFindUserAsync(userRepository, tx.UserId, cancellationToken);
        await TryUpdateUserSnapshotAsync(
            userRepository,
            user,
            plan.Id,
            SubscriptionStatus.Active.ToString(),
            periodEnd,
            plan.MonthlyAiTokenQuota,
            cancellationToken);

        await TryAddAuditLogAsync(
            userRepository,
            tx.UserId,
            tx.Username,
            "SubscriptionUpgraded",
            auditDetails,
            remoteIpAddress,
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
            user?.Role ?? roleHint,
            tx.Username,
            cancellationToken);

        return new ConfirmPaymentResponseDto(
            true,
            $"{plan.Name} paketi başarıyla aktifleştirildi!",
            tx.ToDto(),
            summary,
            updatedUserDto,
            refreshedToken);
    }
}
