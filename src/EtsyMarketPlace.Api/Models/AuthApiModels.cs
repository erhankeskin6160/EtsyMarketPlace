global using EtsyMarketPlace.Application.Auth;

namespace EtsyMarketPlace.Api.Models;

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
