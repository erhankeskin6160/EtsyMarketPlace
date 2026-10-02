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
    DateTimeOffset? LastLoginAt);

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
}
