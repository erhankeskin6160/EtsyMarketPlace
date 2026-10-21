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
using DomainUser = EtsyMarketPlace.Domain.Entities.AppUser;
using DomainAuditLog = EtsyMarketPlace.Domain.Entities.AuditLog;
using AppUserRecord = EtsyMarketPlace.Application.Auth.AppUser;

public sealed class EfPostgreSqlUserRepository : IUserRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlUserRepository> _logger;

    public EfPostgreSqlUserRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlUserRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static DateTimeOffset ToUtcOffset(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public async Task<AppUserRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<AppUserRecord?> GetByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        string normalized = identifier.Trim().ToLowerInvariant();
        var entity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<AppUserRecord>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToRecord).ToList();
    }

    public async Task CreateUserAsync(AppUserRecord user, CancellationToken cancellationToken = default)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

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
            CreatedAt = user.CreatedAt.UtcDateTime,
            LastLoginAt = user.LastLoginAt?.UtcDateTime
        };

        _context.Users.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(AppUserRecord user, CancellationToken cancellationToken = default)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));

        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        if (entity is null) return;

        entity.Email = user.Email;
        entity.Role = user.Role;
        entity.AssignedShopIds = JsonSerializer.Serialize(user.AssignedShopIds ?? []);
        entity.MonthlyAiTokenQuota = user.MonthlyAiTokenQuota;
        entity.IsActive = user.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(string id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (entity is null) return;

        entity.LastLoginAt = lastLoginAt.UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry == null) return;

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
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken cancellationToken = default)
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
            u.LastLoginAt.HasValue ? ToUtcOffset(u.LastLoginAt.Value) : null);
    }
}
