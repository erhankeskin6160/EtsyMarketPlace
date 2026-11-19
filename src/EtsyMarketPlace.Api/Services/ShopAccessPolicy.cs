namespace EtsyMarketPlace.Api.Services;

using System;
using System.Linq;
using System.Security.Claims;
using EtsyMarketPlace.Application.Auth;
using Microsoft.Extensions.Configuration;

public sealed class ShopAccessPolicy(IConfiguration configuration)
{
    private const string LegacyTypoShopId = "523236321";
    private const string CanonicalDefaultShopId = "53236321";

    public bool IsAllowed(string shopId) => IsAllowed(shopId, user: null);

    public bool IsAllowed(string shopId, ClaimsPrincipal? user)
    {
        var normalizedShopId = NormalizeShopId(shopId);
        if (string.IsNullOrWhiteSpace(normalizedShopId))
            return false;

        if (user is not null)
        {
            if (user.IsInRole(UserRoles.Admin) || user.IsInRole(UserRoles.SuperAdmin))
                return true;

            var assignedShops = user.FindAll("shop_id")
                .Select(c => NormalizeShopId(c.Value))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToHashSet(StringComparer.Ordinal);

            if (assignedShops.Count > 0)
                return assignedShops.Contains(normalizedShopId);
        }

        var allowed = configuration.GetSection("Security:AllowedShopIds").Get<string[]>();
        if (allowed is null || allowed.Length == 0)
            return true;

        return allowed
            .Select(NormalizeShopId)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Contains(normalizedShopId, StringComparer.Ordinal);
    }

    private static string NormalizeShopId(string? shopId)
    {
        var trimmed = (shopId ?? string.Empty).Trim();
        return string.Equals(trimmed, LegacyTypoShopId, StringComparison.Ordinal)
            ? CanonicalDefaultShopId
            : trimmed;
    }
}
