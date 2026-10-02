namespace EtsyMarketPlace.Api.Models;

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
