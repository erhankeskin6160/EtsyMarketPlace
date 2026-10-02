namespace EtsyMarketPlace.Application.Auth;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task CreateUserAsync(AppUser user, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(AppUser user, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(string id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default);
    Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken cancellationToken = default);
}
