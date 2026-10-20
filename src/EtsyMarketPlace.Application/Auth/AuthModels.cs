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
    string? LastLoginAt);

public sealed record UpdateUserRequest(
    string Email,
    string Role,
    List<string> AssignedShopIds,
    int MonthlyAiTokenQuota,
    bool IsActive);

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
