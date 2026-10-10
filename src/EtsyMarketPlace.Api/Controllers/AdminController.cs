using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.Auth;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly JwtTokenService _jwtService;

    public AdminController(IUserRepository userRepo, JwtTokenService jwtService)
    {
        _userRepo = userRepo;
        _jwtService = jwtService;
    }

    private (bool IsValid, string? AdminId, string? AdminName) ValidateAdmin()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authHeader["Bearer ".Length..].Trim() : null;
        var (isValid, _, adminId, role, adminName) = _jwtService.ValidateToken(token);
        if (!isValid || role != UserRoles.Admin)
        {
            return (false, null, null);
        }
        return (true, adminId, adminName);
    }

    [HttpGet("users")]
    [EndpointSummary("Kullanıcı Listesi")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        var (isAdmin, _, _) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var users = await _userRepo.GetAllUsersAsync(cancellationToken);
        var dtos = users.Select(u => new UserDto(u.Id, u.Username, u.Email, u.Role, u.AssignedShopIds, u.MonthlyAiTokenQuota, u.UsedAiTokens, u.IsActive, u.CreatedAt.ToString("O"), u.LastLoginAt?.ToString("O"))).ToList();
        return Ok(dtos);
    }

    [HttpPut("users/{id}")]
    [EndpointSummary("Kullanıcı Düzenleme & Yetki/Mağaza Atama")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var (isAdmin, adminId, adminName) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var existingUser = await _userRepo.GetByIdAsync(id, cancellationToken);
        if (existingUser == null)
        {
            return NotFound(new { error = "Kullanıcı bulunamadı." });
        }

        var updated = existingUser with
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            Role = request.Role,
            AssignedShopIds = request.AssignedShopIds ?? existingUser.AssignedShopIds,
            MonthlyAiTokenQuota = request.MonthlyAiTokenQuota,
            IsActive = request.IsActive
        };

        await _userRepo.UpdateUserAsync(updated, cancellationToken);
        await _userRepo.AddAuditLogAsync(new AuditLogEntry(0, adminId ?? "admin", adminName ?? "Admin", "UpdateUser", $"Kullanıcı güncellendi: {existingUser.Username} ({updated.Role})", HttpContext.Connection.RemoteIpAddress?.ToString(), DateTimeOffset.UtcNow), cancellationToken);

        return Ok(new { success = true, message = "Kullanıcı başarıyla güncellendi." });
    }

    [HttpPost("users/{id}/toggle-status")]
    [EndpointSummary("Kullanıcı Durumu Değiştirme (Aktif/Askıda)")]
    public async Task<IActionResult> ToggleUserStatus(string id, CancellationToken cancellationToken)
    {
        var (isAdmin, adminId, adminName) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var existingUser = await _userRepo.GetByIdAsync(id, cancellationToken);
        if (existingUser == null)
        {
            return NotFound(new { error = "Kullanıcı bulunamadı." });
        }

        var updated = existingUser with { IsActive = !existingUser.IsActive };
        await _userRepo.UpdateUserAsync(updated, cancellationToken);
        await _userRepo.AddAuditLogAsync(new AuditLogEntry(0, adminId ?? "admin", adminName ?? "Admin", "ToggleStatus", $"Kullanıcı durumu değiştirildi: {existingUser.Username} (Aktif: {updated.IsActive})", HttpContext.Connection.RemoteIpAddress?.ToString(), DateTimeOffset.UtcNow), cancellationToken);

        return Ok(new { success = true, isActive = updated.IsActive });
    }

    [HttpGet("audit-logs")]
    [EndpointSummary("Audit Güvenlik Günlüğü")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var (isAdmin, _, _) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var logs = await _userRepo.GetAuditLogsAsync(Math.Clamp(limit, 1, 500), cancellationToken);
        var dtos = logs.Select(l => new AuditLogDto(l.Id, l.UserId, l.Username, l.Action, l.Details, l.IpAddress, l.Timestamp.ToString("O"))).ToList();
        return Ok(dtos);
    }

    [HttpGet("system-stats")]
    [EndpointSummary("Sistem ve Kullanıcı İstatistikleri")]
    public async Task<IActionResult> GetSystemStats(CancellationToken cancellationToken)
    {
        var (isAdmin, _, _) = ValidateAdmin();
        if (!isAdmin) return StatusCode(StatusCodes.Status403Forbidden);

        var users = await _userRepo.GetAllUsersAsync(cancellationToken);
        var logs = await _userRepo.GetAuditLogsAsync(1000, cancellationToken);

        var totalUsers = users.Count;
        var activeUsers = users.Count(u => u.IsActive);
        var totalShops = users.SelectMany(u => u.AssignedShopIds).Distinct().Count();
        var totalUsedTokens = users.Sum(u => (long)u.UsedAiTokens);

        return Ok(new SystemStatsDto(totalUsers, activeUsers, totalShops, totalUsedTokens, logs.Count));
    }
}
