namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.Identity;
using DomainAuditLog = EtsyMarketPlace.Domain.Entities.AuditLog;
using AppUserRecord = EtsyMarketPlace.Application.Auth.AppUser;

/// <summary>
/// PostgreSQL 16 EF Core + ASP.NET Core Identity tabanlı kullanıcı ve audit günlüğü deposu.
/// PostgreSQL erişilemez olduğunda yerel SQLite yedeğine (<see cref="SqliteEtsyIntegrationStore"/>) düşer.
/// </summary>
public sealed class EfPostgreSqlUserRepository : IUserRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly UserManager<ApplicationIdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EfPostgreSqlUserRepository> _logger;
    private readonly SqliteEtsyIntegrationStore? _fallbackStore;

    public EfPostgreSqlUserRepository(
        EtsyMarketPlaceDbContext context,
        UserManager<ApplicationIdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IMemoryCache cache,
        ILogger<EfPostgreSqlUserRepository> logger,
        SqliteEtsyIntegrationStore? fallbackStore = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fallbackStore = fallbackStore;
    }

    public Task<AppUserRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackAsync<AppUserRecord?>(
            async () =>
            {
                var entity = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
                if (entity is null) return null;

                var roles = await GetRoleNamesAsync(entity.Id, cancellationToken);
                return IdentityUserMapper.ToRecord(entity, roles);
            },
            store => store.GetByIdAsync(id, cancellationToken),
            $"GetById({id})");
    }

    public Task<AppUserRecord?> GetByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return Task.FromResult<AppUserRecord?>(null);

        var normalized = identifier.Trim().ToUpperInvariant();
        return ExecuteWithFallbackAsync<AppUserRecord?>(
            async () =>
            {
                var entity = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized,
                        cancellationToken);
                if (entity is null) return null;

                var roles = await GetRoleNamesAsync(entity.Id, cancellationToken);
                return IdentityUserMapper.ToRecord(entity, roles);
            },
            store => store.GetByUsernameOrEmailAsync(identifier, cancellationToken),
            "GetByUsernameOrEmail");
    }

    public Task<IReadOnlyList<AppUserRecord>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackAsync<IReadOnlyList<AppUserRecord>>(
            async () =>
            {
                var entities = await _context.Users
                    .AsNoTracking()
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync(cancellationToken);

                var userRolePairs = await (
                    from ur in _context.UserRoles.AsNoTracking()
                    join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                    select new { ur.UserId, RoleName = r.Name ?? string.Empty }
                ).ToListAsync(cancellationToken);

                var rolesByUser = userRolePairs
                    .GroupBy(x => x.UserId)
                    .ToDictionary(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).Where(n => !string.IsNullOrEmpty(n)).ToList());

                return entities.Select(u =>
                {
                    rolesByUser.TryGetValue(u.Id, out var roles);
                    return IdentityUserMapper.ToRecord(u, roles);
                }).ToList();
            },
            store => store.GetAllUsersAsync(cancellationToken),
            "GetAllUsers");
    }

    public Task CreateUserAsync(AppUserRecord user, CancellationToken cancellationToken = default)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        return ExecuteWithFallbackAsync(
            async () =>
            {
                var entity = new ApplicationIdentityUser
                {
                    Id = string.IsNullOrWhiteSpace(user.Id) ? Guid.NewGuid().ToString("N") : user.Id,
                    UserName = user.Username,
                    Email = user.Email,
                    EmailConfirmed = true,
                    PrimaryRole = user.Role,
                    AssignedShopIds = JsonSerializer.Serialize(user.AssignedShopIds ?? []),
                    MonthlyAiTokenQuota = user.MonthlyAiTokenQuota,
                    UsedAiTokens = user.UsedAiTokens,
                    IsActive = user.IsActive,
                    SubscriptionPlanId = user.SubscriptionPlanId,
                    SubscriptionStatus = user.SubscriptionStatus,
                    SubscriptionExpiresAt = user.SubscriptionExpiresAt?.UtcDateTime,
                    CreatedAt = user.CreatedAt.UtcDateTime,
                    LastLoginAt = user.LastLoginAt?.UtcDateTime,
                    LockoutEnabled = true,
                    AccessFailedCount = 0,
                    LegacyPasswordHash = string.IsNullOrWhiteSpace(user.PasswordSalt) ? null : user.PasswordHash,
                    LegacyPasswordSalt = string.IsNullOrWhiteSpace(user.PasswordSalt) ? null : user.PasswordSalt
                };

                var createRes = await _userManager.CreateAsync(entity);
                if (!createRes.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", createRes.Errors.Select(e => e.Description)));
                }

                var targetRole = string.IsNullOrWhiteSpace(user.Role) ? UserRoles.StoreOwner : user.Role;
                if (!await _roleManager.RoleExistsAsync(targetRole))
                {
                    await _roleManager.CreateAsync(new IdentityRole(targetRole));
                }
                await _userManager.AddToRoleAsync(entity, targetRole);
                return true;
            },
            async store =>
            {
                await store.CreateUserAsync(user, cancellationToken);
                return true;
            },
            $"CreateUser({user.Username})");
    }

    public Task UpdateUserAsync(AppUserRecord user, CancellationToken cancellationToken = default)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        return ExecuteWithFallbackAsync(
            async () =>
            {
                var entity = await _userManager.FindByIdAsync(user.Id);
                if (entity is null) return true;

                var roleChanged = !string.Equals(entity.PrimaryRole, user.Role, StringComparison.OrdinalIgnoreCase);
                var deactivated = entity.IsActive && !user.IsActive;

                if (!string.Equals(entity.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    await _userManager.SetEmailAsync(entity, user.Email);
                }

                entity.PrimaryRole = user.Role;
                entity.AssignedShopIds = JsonSerializer.Serialize(user.AssignedShopIds ?? []);
                entity.MonthlyAiTokenQuota = user.MonthlyAiTokenQuota;
                entity.IsActive = user.IsActive;
                entity.SubscriptionPlanId = user.SubscriptionPlanId;
                entity.SubscriptionStatus = user.SubscriptionStatus;
                entity.SubscriptionExpiresAt = user.SubscriptionExpiresAt?.UtcDateTime;

                await _userManager.UpdateAsync(entity);

                if (roleChanged && !string.IsNullOrWhiteSpace(user.Role))
                {
                    var currentRoles = await _userManager.GetRolesAsync(entity);
                    if (currentRoles.Count > 0)
                    {
                        await _userManager.RemoveFromRolesAsync(entity, currentRoles);
                    }

                    if (!await _roleManager.RoleExistsAsync(user.Role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(user.Role));
                    }

                    await _userManager.AddToRoleAsync(entity, user.Role);
                }

                // Rol değişikliği veya hesap askıya alma durumunda mevcut tüm JWT token'ları anında iptal et
                if (roleChanged || deactivated)
                {
                    await _userManager.UpdateSecurityStampAsync(entity);
                    _cache.Remove(IdentityUserMapper.GetStampCacheKey(entity.Id));
                }

                return true;
            },
            async store =>
            {
                await store.UpdateUserAsync(user, cancellationToken);
                return true;
            },
            $"UpdateUser({user.Id})");
    }

    public Task UpdateLastLoginAsync(string id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackAsync(
            async () =>
            {
                var entity = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

                if (entity is null) return true;

                entity.LastLoginAt = lastLoginAt.UtcDateTime;
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            },
            async store =>
            {
                await store.UpdateLastLoginAsync(id, lastLoginAt, cancellationToken);
                return true;
            },
            $"UpdateLastLogin({id})");
    }

    public Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null) return Task.CompletedTask;

        return ExecuteWithFallbackAsync(
            async () =>
            {
                var entity = new DomainAuditLog
                {
                    UserId = entry.UserId ?? string.Empty,
                    Username = entry.Username ?? string.Empty,
                    Action = entry.Action ?? string.Empty,
                    Details = entry.Details,
                    IpAddress = entry.IpAddress,
                    Timestamp = entry.Timestamp.UtcDateTime
                };

                _context.AuditLogs.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            },
            async store =>
            {
                await store.AddAuditLogAsync(entry, cancellationToken);
                return true;
            },
            $"AddAuditLog({entry.Action})");
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackAsync<IReadOnlyList<AuditLogEntry>>(
            async () =>
            {
                var entities = await _context.AuditLogs
                    .AsNoTracking()
                    .OrderByDescending(x => x.Timestamp)
                    .Take(limit)
                    .ToListAsync(cancellationToken);

                return entities.Select(e => new AuditLogEntry(
                    e.Id,
                    e.UserId,
                    e.Username,
                    e.Action,
                    e.Details,
                    e.IpAddress,
                    IdentityUserMapper.ToUtcOffset(e.Timestamp))).ToList();
            },
            store => store.GetAuditLogsAsync(limit, cancellationToken),
            "GetAuditLogs");
    }

    private async Task<IList<string>> GetRoleNamesAsync(string userId, CancellationToken ct)
    {
        return await (
            from ur in _context.UserRoles.AsNoTracking()
            join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.UserId == userId && r.Name != null
            select r.Name!
        ).ToListAsync(ct);
    }

    private async Task<T> ExecuteWithFallbackAsync<T>(
        Func<Task<T>> pgAction,
        Func<SqliteEtsyIntegrationStore, Task<T>> sqliteAction,
        string operationName)
    {
        try
        {
            return await pgAction();
        }
        catch (Exception ex) when (_fallbackStore is not null && IsDatabaseUnavailableException(ex))
        {
            _context.ChangeTracker.Clear();
            _logger.LogWarning(ex, "PostgreSQL {Operation} başarısız; SQLite kullanıcı yedeği kullanılıyor: {Message}", operationName, ex.Message);
            return await sqliteAction(_fallbackStore);
        }
    }

    private static bool IsDatabaseUnavailableException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.NpgsqlException ||
                current is System.Net.Sockets.SocketException ||
                current is TimeoutException ||
                current.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
