namespace EtsyMarketPlace.Application.Auth;

public sealed record AppUser(
    string Id,
    string Username,
    string Email,
    string PasswordHash,
    string PasswordSalt,
    string Role,
    List<string> AssignedShopIds,
    int MonthlyAiTokenQuota,
    int UsedAiTokens,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string SubscriptionPlanId = "pro",
    string SubscriptionStatus = "Active",
    DateTimeOffset? SubscriptionExpiresAt = null);

public sealed record AuditLogEntry(
    long Id,
    string UserId,
    string Username,
    string Action,
    string? Details,
    string? IpAddress,
    DateTimeOffset Timestamp);

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string StoreOwner = "StoreOwner";
    public const string Demo = "Demo";
    public const string Operator = "Operator";
}

public sealed record LoginRequest(string UsernameOrEmail, string Password);

public sealed record RegisterRequest(string Username, string Email, string Password, string? ShopId);

public sealed record AuthResponse(bool Success, string? Token, string? Message, UserDto? User);

public sealed record UserDto(
    string Id,
    string Username,
    string Email,
    string Role,
    List<string> AssignedShopIds,
    int MonthlyAiTokenQuota,
    int UsedAiTokens,
    bool IsActive,
    string CreatedAt,
    string? LastLoginAt,
    string SubscriptionPlanId = "pro",
    string SubscriptionStatus = "Active",
    string? SubscriptionExpiresAt = null);

public sealed record UpdateUserRequest(
    string Email,
    string Role,
    List<string> AssignedShopIds,
    int MonthlyAiTokenQuota,
    bool IsActive,
    string? SubscriptionPlanId = null);

public sealed record AuditLogDto(
    long Id,
    string UserId,
    string Username,
    string Action,
    string? Details,
    string? IpAddress,
    string Timestamp);

public sealed record SystemStatsDto(
    int TotalUsers,
    int ActiveUsers,
    int TotalShops,
    long TotalUsedAiTokens,
    int AuditLogCount);

public static class AuthUserExtensions
{
    public static UserDto ToDto(this AppUser user, DateTimeOffset? lastLoginOverride = null)
        => new(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.AssignedShopIds,
            user.MonthlyAiTokenQuota,
            user.UsedAiTokens,
            user.IsActive,
            user.CreatedAt.ToString("O"),
            (lastLoginOverride ?? user.LastLoginAt)?.ToString("O"),
            user.SubscriptionPlanId,
            user.SubscriptionStatus,
            user.SubscriptionExpiresAt?.ToString("O"));
}
