using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.Features.Auth.Commands.Login;
using EtsyMarketPlace.Application.Features.Auth.Commands.Logout;
using EtsyMarketPlace.Application.Features.Auth.Commands.Register;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Auth;

public sealed class LogoutAndIdentityAuthTests
{
    private const string Secret = "Test_Enterprise_Secret_Key_For_Identity_Auth_1234567890!";

    private sealed class InMemoryUserRepo : IUserRepository
    {
        public List<AppUser> Users { get; } = [];
        public List<AuditLogEntry> AuditLogs { get; } = [];
        public DateTimeOffset? LastLoginUpdatedAt { get; private set; }

        public Task<AppUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<AppUser?> GetByUsernameOrEmailAsync(string identifier, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u =>
                string.Equals(u.Username, identifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.Email, identifier, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<AppUser>> GetAllUsersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AppUser>>(Users.ToList());

        public Task CreateUserAsync(AppUser user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateUserAsync(AppUser user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateLastLoginAsync(string id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default)
        {
            LastLoginUpdatedAt = lastLoginAt;
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
        {
            AuditLogs.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLogEntry>>(AuditLogs.Take(limit).ToList());
    }

    private sealed class FakeIdentityAuthService : IIdentityAuthService
    {
        public Func<string, string, IdentityOperationResult<AppUser>>? OnAuthenticate { get; set; }
        public Func<string, string, string, string?, IdentityOperationResult<AppUser>>? OnRegister { get; set; }
        public List<string> LoggedOutUserIds { get; } = [];
        public Dictionary<string, string> ActiveStamps { get; } = new();

        public Task<IdentityOperationResult<AppUser>> AuthenticateAsync(
            string usernameOrEmail,
            string password,
            string? remoteIpAddress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(OnAuthenticate?.Invoke(usernameOrEmail, password)
                ?? IdentityOperationResult<AppUser>.Fail("Geçersiz kullanıcı adı veya şifre.", HttpStatusCode.Unauthorized));

        public Task<IdentityOperationResult<AppUser>> RegisterAsync(
            string username,
            string email,
            string password,
            string? shopId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(OnRegister?.Invoke(username, email, password, shopId)
                ?? IdentityOperationResult<AppUser>.Fail("Hata", HttpStatusCode.BadRequest));

        public Task<bool> LogoutAsync(string userId, CancellationToken cancellationToken = default)
        {
            LoggedOutUserIds.Add(userId);
            ActiveStamps[userId] = Guid.NewGuid().ToString("N");
            return Task.FromResult(true);
        }

        public Task<bool> ValidateSecurityStampAsync(string userId, string? securityStamp, CancellationToken cancellationToken = default)
            => Task.FromResult(
                !string.IsNullOrWhiteSpace(securityStamp) &&
                ActiveStamps.TryGetValue(userId, out var current) &&
                string.Equals(current, securityStamp, StringComparison.Ordinal));

        public void InvalidateSecurityStampCache(string userId) { }
    }

    private static AppUser CreateTestUser(string role = UserRoles.Admin, string? stamp = "stamp-001") => new(
        "user-42",
        "admin",
        "admin@test.local",
        "identity-hash",
        string.Empty,
        role,
        ["53236321"],
        1_000_000,
        0,
        true,
        DateTimeOffset.UtcNow,
        null,
        "enterprise",
        "Active",
        null,
        stamp);

    [Fact]
    public async Task LogoutUserHandler_ShouldRotateSecurityStampAndWriteAuditLog()
    {
        var repo = new InMemoryUserRepo();
        var identity = new FakeIdentityAuthService();
        identity.ActiveStamps["user-42"] = "stamp-initial";

        var handler = new LogoutUserHandler(repo, identity);
        var response = await handler.Handle(new LogoutUserRequest
        {
            UserId = "user-42",
            Username = "admin",
            RemoteIpAddress = "10.0.0.5"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("user-42", identity.LoggedOutUserIds);
        Assert.NotEqual("stamp-initial", identity.ActiveStamps["user-42"]);
        Assert.Single(repo.AuditLogs);
        Assert.Equal("Logout", repo.AuditLogs[0].Action);
        Assert.Equal("10.0.0.5", repo.AuditLogs[0].IpAddress);
    }

    [Fact]
    public async Task LogoutUserHandler_WithEmptyUserId_ShouldReturnUnauthorized()
    {
        var repo = new InMemoryUserRepo();
        var identity = new FakeIdentityAuthService();
        var handler = new LogoutUserHandler(repo, identity);

        var response = await handler.Handle(new LogoutUserRequest { UserId = "" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal((int)HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(identity.LoggedOutUserIds);
    }

    [Fact]
    public async Task LoginUserHandler_WithIdentityService_ShouldEmbedSecurityStampInJwt()
    {
        var repo = new InMemoryUserRepo();
        var jwt = new JwtTokenService(Secret);
        var identity = new FakeIdentityAuthService
        {
            OnAuthenticate = (_, _) => IdentityOperationResult<AppUser>.Ok(CreateTestUser(UserRoles.Admin, "stamp-abc-999"))
        };

        var handler = new LoginUserHandler(repo, jwt, identity);
        var response = await handler.Handle(new LoginUserRequest
        {
            UsernameOrEmail = "admin",
            Password = "Admin123*!",
            RemoteIpAddress = "127.0.0.1"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var auth = Assert.IsType<AuthResponse>(response.Data);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));

        var (isValid, principal, userId, role, username) = jwt.ValidateToken(auth.Token);
        Assert.True(isValid);
        Assert.Equal("user-42", userId);
        Assert.Equal(UserRoles.Admin, role);
        Assert.Equal("admin", username);
        Assert.Equal("stamp-abc-999", principal?.FindFirst("sec_stamp")?.Value);
        Assert.False(string.IsNullOrWhiteSpace(principal?.FindFirst("jti")?.Value));
        Assert.NotNull(repo.LastLoginUpdatedAt);
        Assert.Contains(repo.AuditLogs, a => a.Action == "Login");
    }

    [Fact]
    public async Task LoginUserHandler_WhenAccountLockedOut_ShouldReturnLockedStatus()
    {
        var repo = new InMemoryUserRepo();
        var jwt = new JwtTokenService(Secret);
        var identity = new FakeIdentityAuthService
        {
            OnAuthenticate = (_, _) => IdentityOperationResult<AppUser>.Fail("Hesabınız kilitlenmiştir.", HttpStatusCode.Locked)
        };

        var handler = new LoginUserHandler(repo, jwt, identity);
        var response = await handler.Handle(new LoginUserRequest
        {
            UsernameOrEmail = "admin",
            Password = "wrong"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal((int)HttpStatusCode.Locked, response.StatusCode);
    }

    [Fact]
    public async Task RegisterUserHandler_WithIdentityService_ShouldCreateUserAndReturnToken()
    {
        var repo = new InMemoryUserRepo();
        var jwt = new JwtTokenService(Secret);
        var identity = new FakeIdentityAuthService
        {
            OnRegister = (u, e, _, _) => IdentityOperationResult<AppUser>.Ok(CreateTestUser(UserRoles.StoreOwner, "stamp-new-1") with
            {
                Username = u,
                Email = e
            })
        };

        var handler = new RegisterUserHandler(repo, jwt, identity);
        var response = await handler.Handle(new RegisterUserRequest
        {
            Username = "newstore",
            Email = "newstore@etsy.com",
            Password = "Password123!"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var auth = Assert.IsType<AuthResponse>(response.Data);
        var (isValid, principal, _, _, _) = jwt.ValidateToken(auth.Token);
        Assert.True(isValid);
        Assert.Equal("stamp-new-1", principal?.FindFirst("sec_stamp")?.Value);
        Assert.Contains(repo.AuditLogs, a => a.Action == "Register");
    }

    [Fact]
    public void JwtTokenService_SuperAdminRole_ShouldAlsoCarryAdminRoleClaim()
    {
        var jwt = new JwtTokenService(Secret);
        var user = CreateTestUser(UserRoles.SuperAdmin, "stamp-sa");

        var token = jwt.GenerateToken(user);
        var (isValid, principal, _, _, _) = jwt.ValidateToken(token);

        Assert.True(isValid);
        Assert.NotNull(principal);
        Assert.True(principal.IsInRole(UserRoles.SuperAdmin));
        Assert.True(principal.IsInRole(UserRoles.Admin));
    }
}
