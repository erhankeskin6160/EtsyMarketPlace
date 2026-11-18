namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using DomainUser = EtsyMarketPlace.Domain.Entities.AppUser;
using DomainAuditLog = EtsyMarketPlace.Domain.Entities.AuditLog;
using AppUserRecord = EtsyMarketPlace.Application.Auth.AppUser;

/// <summary>
/// PostgreSQL 16 EF Core tabanlı kullanıcı ve audit günlüğü deposu.
/// PostgreSQL erişilemez olduğunda yerel SQLite yedeğine (<see cref="SqliteEtsyIntegrationStore"/>) düşer.
/// </summary>
public sealed class EfPostgreSqlUserRepository : IUserRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlUserRepository> _logger;
    private readonly SqliteEtsyIntegrationStore? _fallbackStore;

    public EfPostgreSqlUserRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlUserRepository> logger,
        SqliteEtsyIntegrationStore? fallbackStore = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fallbackStore = fallbackStore;
    }

    private static DateTimeOffset ToUtcOffset(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public Task<AppUserRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return ExecuteWithFallbackAsync<AppUserRecord?>(
            async () =>
            {
                var entity = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
                return entity is null ? null : ToRecord(entity);
            },
            store => store.GetByIdAsync(id, cancellationToken),
            $"GetById({id})");
    }

    public Task<AppUserRecord?> GetByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return Task.FromResult<AppUserRecord?>(null);

        string normalized = identifier.Trim().ToLowerInvariant();
        return ExecuteWithFallbackAsync<AppUserRecord?>(
            async () =>
            {
                var entity = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized, cancellationToken);
                return entity is null ? null : ToRecord(entity);
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
                return entities.Select(ToRecord).ToList();
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
                var entity = new DomainUser
                {
                    Id = string.IsNullOrWhiteSpace(user.Id) ? Guid.NewGuid().ToString("N") : user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    PasswordHash = user.PasswordHash,
                    PasswordSalt = user.PasswordSalt,
                    Role = user.Role,
                    AssignedShopIds = JsonSerializer.Serialize(user.AssignedShopIds ?? []),
                    MonthlyAiTokenQuota = user.MonthlyAiTokenQuota,
                    UsedAiTokens = user.UsedAiTokens,
                    IsActive = user.IsActive,
                    SubscriptionPlanId = user.SubscriptionPlanId,
                    SubscriptionStatus = user.SubscriptionStatus,
                    SubscriptionExpiresAt = user.SubscriptionExpiresAt?.UtcDateTime,
                    CreatedAt = user.CreatedAt.UtcDateTime,
                    LastLoginAt = user.LastLoginAt?.UtcDateTime
                };

                _context.Users.Add(entity);
                await _context.SaveChangesAsync(cancellationToken);
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
                var entity = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

                if (entity is null) return true;

                entity.Email = user.Email;
                entity.Role = user.Role;
                entity.AssignedShopIds = JsonSerializer.Serialize(user.AssignedShopIds ?? []);
                entity.MonthlyAiTokenQuota = user.MonthlyAiTokenQuota;
                entity.IsActive = user.IsActive;
                entity.SubscriptionPlanId = user.SubscriptionPlanId;
                entity.SubscriptionStatus = user.SubscriptionStatus;
                entity.SubscriptionExpiresAt = user.SubscriptionExpiresAt?.UtcDateTime;

                await _context.SaveChangesAsync(cancellationToken);
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
                    ToUtcOffset(e.Timestamp))).ToList();
            },
            store => store.GetAuditLogsAsync(limit, cancellationToken),
            "GetAuditLogs");
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

    private static AppUserRecord ToRecord(DomainUser u)
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

        return new AppUserRecord(
            u.Id,
            u.Username,
            u.Email,
            u.PasswordHash,
            u.PasswordSalt,
            u.Role,
            shopIds,
            u.MonthlyAiTokenQuota,
            u.UsedAiTokens,
            u.IsActive,
            ToUtcOffset(u.CreatedAt),
            u.LastLoginAt.HasValue ? ToUtcOffset(u.LastLoginAt.Value) : null,
            string.IsNullOrWhiteSpace(u.SubscriptionPlanId) ? "pro" : u.SubscriptionPlanId,
            string.IsNullOrWhiteSpace(u.SubscriptionStatus) ? "Active" : u.SubscriptionStatus,
            u.SubscriptionExpiresAt.HasValue ? ToUtcOffset(u.SubscriptionExpiresAt.Value) : null);
    }
}
