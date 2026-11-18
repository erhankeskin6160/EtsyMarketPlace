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
}
