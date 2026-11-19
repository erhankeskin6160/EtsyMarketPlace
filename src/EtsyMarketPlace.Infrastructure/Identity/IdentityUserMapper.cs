using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EtsyMarketPlace.Application.Auth;
using AppUserRecord = EtsyMarketPlace.Application.Auth.AppUser;

namespace EtsyMarketPlace.Infrastructure.Identity;

internal static class IdentityUserMapper
{
    public const string SecurityStampCachePrefix = "identity:sec_stamp:";

    public static string GetStampCacheKey(string userId) => $"{SecurityStampCachePrefix}{userId}";

    public static DateTimeOffset ToUtcOffset(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Local
            ? dt.ToUniversalTime()
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public static AppUserRecord ToRecord(ApplicationIdentityUser u, IList<string>? identityRoles = null)
    {
        List<string> shopIds;
        try
        {
            shopIds = JsonSerializer.Deserialize<List<string>>(u.AssignedShopIds) ?? [];
        }
        catch
        {
            shopIds = [];
        }

        var role = ResolvePrimaryRole(u.PrimaryRole, identityRoles);

        return new AppUserRecord(
            u.Id,
            u.UserName ?? string.Empty,
            u.Email ?? string.Empty,
            u.PasswordHash ?? string.Empty,
            string.Empty,
            role,
            shopIds,
            u.MonthlyAiTokenQuota,
            u.UsedAiTokens,
            u.IsActive,
            ToUtcOffset(u.CreatedAt),
            u.LastLoginAt.HasValue ? ToUtcOffset(u.LastLoginAt.Value) : null,
            string.IsNullOrWhiteSpace(u.SubscriptionPlanId) ? "pro" : u.SubscriptionPlanId,
            string.IsNullOrWhiteSpace(u.SubscriptionStatus) ? "Active" : u.SubscriptionStatus,
            u.SubscriptionExpiresAt.HasValue ? ToUtcOffset(u.SubscriptionExpiresAt.Value) : null,
            u.SecurityStamp);
    }

    private static string ResolvePrimaryRole(string? primaryRole, IList<string>? identityRoles)
    {
        if (identityRoles is { Count: > 0 })
        {
            if (identityRoles.Any(r => string.Equals(r, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)))
                return UserRoles.Admin;
            if (identityRoles.Any(r => string.Equals(r, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
                return UserRoles.Admin;
            return identityRoles[0];
        }

        if (string.Equals(primaryRole, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return UserRoles.Admin;

        return string.IsNullOrWhiteSpace(primaryRole) ? UserRoles.StoreOwner : primaryRole;
    }
}
