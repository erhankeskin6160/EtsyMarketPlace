using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;

namespace EtsyMarketPlace.Application.Subscriptions;

public sealed class SubscriptionEntitlementService(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository) : ISubscriptionEntitlementService
{
    public async Task<UserSubscriptionSummaryDto> GetEntitlementSummaryAsync(
        string userId,
        string? roleHint = null,
        string? usernameHint = null,
        CancellationToken cancellationToken = default)
    {
        var user = await TryGetUserAsync(userId, cancellationToken);
        var role = user?.Role ?? roleHint ?? UserRoles.StoreOwner;
        var username = user?.Username ?? usernameHint ?? "Kullanıcı";
        var isAdminExempt = IsAdminRole(role);

        var plans = await subscriptionRepository.GetAllPlansAsync(activeOnly: false, cancellationToken);
        if (plans.Count == 0)
        {
            plans = SubscriptionFeatureKeys.GetDefaultPlans();
        }

        var userSub = await subscriptionRepository.GetUserSubscriptionAsync(userId, cancellationToken);

        var effectivePlanId = isAdminExempt
            ? SubscriptionPlanCode.Enterprise
            : SubscriptionPlanCode.Normalize(
                userSub?.PlanId ?? user?.SubscriptionPlanId ?? SubscriptionPlanCode.Pro);

        var plan = plans.FirstOrDefault(p => string.Equals(p.Id, effectivePlanId, StringComparison.OrdinalIgnoreCase))
                   ?? plans.FirstOrDefault(p => p.Id == SubscriptionPlanCode.Pro)
                   ?? plans[0];

        var now = DateTime.UtcNow;
        var startDate = userSub?.StartDateUtc ?? user?.CreatedAt.UtcDateTime ?? now;
        var periodStart = userSub?.CurrentPeriodStartUtc ?? now;
        var periodEnd = userSub?.CurrentPeriodEndUtc
                        ?? user?.SubscriptionExpiresAt?.UtcDateTime
                        ?? now.AddMonths(1);

        var status = isAdminExempt
            ? SubscriptionStatus.Active.ToString()
            : userSub?.Status ?? user?.SubscriptionStatus ?? SubscriptionStatus.Active.ToString();

        if (!isAdminExempt &&
            periodEnd < now &&
            string.Equals(status, SubscriptionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            status = SubscriptionStatus.Expired.ToString();
        }

        var isSubscriptionActive = isAdminExempt ||
                                   string.Equals(status, SubscriptionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(status, SubscriptionStatus.Trialing.ToString(), StringComparison.OrdinalIgnoreCase);

        var maxShops = isAdminExempt
            ? Math.Max(plan.MaxShops, 25)
            : userSub?.CustomMaxShopsOverride ?? plan.MaxShops;

        var monthlyQuota = isAdminExempt
            ? Math.Max(plan.MonthlyAiTokenQuota, 10_000_000)
            : userSub?.CustomAiTokenQuotaOverride
              ?? (user is not null && user.MonthlyAiTokenQuota > 0 ? Math.Max(user.MonthlyAiTokenQuota, plan.MonthlyAiTokenQuota) : plan.MonthlyAiTokenQuota);

        var usedTokens = user?.UsedAiTokens ?? 0;
        var assignedShopsCount = user?.AssignedShopIds.Count ?? 0;
        var remainingShops = Math.Max(0, maxShops - assignedShopsCount);
        var remainingTokens = Math.Max(0, monthlyQuota - usedTokens);
        var usagePct = monthlyQuota > 0
            ? Math.Min(100d, Math.Round((double)usedTokens / monthlyQuota * 100d, 1))
            : 0d;

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((periodEnd - now).TotalDays));

        IReadOnlyList<string> allowedFeatures;
        if (isAdminExempt)
        {
            allowedFeatures = SubscriptionFeatureKeys.EnterpriseFeatures;
        }
        else if (!isSubscriptionActive)
        {
            allowedFeatures = Array.Empty<string>();
        }
        else
        {
            allowedFeatures = SubscriptionFeatureKeys.ParseAllowedFeatures(plan.AllowedFeaturesJson);
        }

        return new UserSubscriptionSummaryDto(
            SubscriptionId: userSub?.Id ?? $"default-{userId}",
            UserId: userId,
            Username: username,
            Role: role,
            PlanId: plan.Id,
            PlanName: plan.Name,
            BadgeText: plan.BadgeText,
            AccentColor: plan.AccentColor,
            Status: status,
            BillingCycle: userSub?.BillingCycle ?? BillingCycle.Monthly.ToString(),
            StartDateUtc: startDate.ToString("O"),
            CurrentPeriodStartUtc: periodStart.ToString("O"),
            CurrentPeriodEndUtc: periodEnd.ToString("O"),
            DaysRemaining: daysRemaining,
            AutoRenew: userSub?.AutoRenew ?? true,
            IsAdminExempt: isAdminExempt,
            MaxShops: maxShops,
            AssignedShopsCount: assignedShopsCount,
            RemainingShopSlots: remainingShops,
            MonthlyAiTokenQuota: monthlyQuota,
            UsedAiTokens: usedTokens,
            RemainingAiTokens: remainingTokens,
            AiUsagePercentage: usagePct,
            MaxMonthlyAiListings: isAdminExempt ? -1 : plan.MaxMonthlyAiListings,
            MaxBatchQueueSize: isAdminExempt ? 250 : plan.MaxBatchQueueSize,
            AllowedFeatures: allowedFeatures);
    }

    public async Task<FeatureAccessCheckDto> CheckFeatureAccessAsync(
        string userId,
        string featureKey,
        string? roleHint = null,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetEntitlementSummaryAsync(userId, roleHint, cancellationToken: cancellationToken);
        var normalizedKey = SubscriptionFeatureKeys.NormalizeFeatureKey(featureKey);
        var requiredPlan = SubscriptionFeatureKeys.GetMinimumRequiredPlan(normalizedKey);

        if (summary.IsAdminExempt)
        {
            return new FeatureAccessCheckDto(
                true,
                featureKey,
                normalizedKey,
                summary.PlanId,
                requiredPlan,
                "Admin rolü tüm modül sınırlarından muaftır.");
        }

        var isActive = string.Equals(summary.Status, SubscriptionStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(summary.Status, SubscriptionStatus.Trialing.ToString(), StringComparison.OrdinalIgnoreCase);

        if (!isActive)
        {
            return new FeatureAccessCheckDto(
                false,
                featureKey,
                normalizedKey,
                summary.PlanId,
                requiredPlan,
                $"Aboneliğiniz aktif değil ({summary.Status}). Devam etmek için paketinizi yenileyin.");
        }

        var allowed = summary.AllowedFeatures.Contains(normalizedKey, StringComparer.OrdinalIgnoreCase);
        var reason = allowed
            ? $"{summary.PlanName} paketi ile bu modüle erişiminiz açıktır."
            : $"Bu özellik en az {requiredPlan.ToUpperInvariant()} paketi gerektirir (Mevcut paket: {summary.PlanName}).";

        return new FeatureAccessCheckDto(allowed, featureKey, normalizedKey, summary.PlanId, requiredPlan, reason);
    }

    public async Task<ShopLimitCheckDto> CheckShopLimitAsync(
        string userId,
        int requestedShopCount = 1,
        string? roleHint = null,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetEntitlementSummaryAsync(userId, roleHint, cancellationToken: cancellationToken);
        if (summary.IsAdminExempt)
        {
            return new ShopLimitCheckDto(
                true,
                summary.MaxShops,
                summary.AssignedShopsCount,
                summary.RemainingShopSlots,
                "Admin rolü mağaza limitinden muaftır.");
        }

        var allowed = summary.AssignedShopsCount + requestedShopCount <= summary.MaxShops;
        var reason = allowed
            ? $"Mağaza slotu uygun ({summary.AssignedShopsCount}/{summary.MaxShops})."
            : $"{summary.PlanName} paketinizde maksimum {summary.MaxShops} mağaza bağlayabilirsiniz. Daha fazla mağaza için paketinizi yükseltin.";

        return new ShopLimitCheckDto(allowed, summary.MaxShops, summary.AssignedShopsCount, summary.RemainingShopSlots, reason);
    }

    public async Task<AiQuotaCheckDto> CheckAiTokenQuotaAsync(
        string userId,
        int requiredTokens = 1,
        string? roleHint = null,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetEntitlementSummaryAsync(userId, roleHint, cancellationToken: cancellationToken);
        if (summary.IsAdminExempt)
        {
            return new AiQuotaCheckDto(
                true,
                summary.MonthlyAiTokenQuota,
                summary.UsedAiTokens,
                summary.RemainingAiTokens,
                "Admin rolü AI kota limitinden muaftır.");
        }

        var allowed = summary.UsedAiTokens + requiredTokens <= summary.MonthlyAiTokenQuota;
        var reason = allowed
            ? $"Kalan AI kotası: {summary.RemainingAiTokens:N0} token."
            : $"{summary.PlanName} paketinizin aylık AI token kotası ({summary.MonthlyAiTokenQuota:N0}) dolmuştur. Lütfen paketinizi yükseltin.";

        return new AiQuotaCheckDto(allowed, summary.MonthlyAiTokenQuota, summary.UsedAiTokens, summary.RemainingAiTokens, reason);
    }

    private async Task<AppUser?> TryGetUserAsync(string userId, CancellationToken cancellationToken)
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

    private static bool IsAdminRole(string? role)
        => string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
}
