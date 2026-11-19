namespace EtsyMarketPlace.Api.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using EtsyMarketPlace.Api.Security;
using EtsyMarketPlace.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class BearerAuthenticationHandlerTests
{
    private const string Secret = "Test_Integration_Secret_Key_For_Bearer_Handler_0123456789";
    private readonly JwtTokenService _jwtTokenService = new(Secret);

    private (BearerAuthenticationHandler handler, DefaultHttpContext context) CreateHandler(
        string? authHeader = null,
        IIdentityAuthService? identityAuthService = null)
    {
        var optionsMonitor = new TestOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        var handler = new BearerAuthenticationHandler(
            optionsMonitor,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            _jwtTokenService,
            identityAuthService);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (authHeader != null)
        {
            context.Request.Headers["Authorization"] = authHeader;
        }

        return (handler, context);
    }

    [Fact]
    public async Task Authenticate_WithValidToken_ReturnsSuccess()
    {
        var user = new AppUser(
            "user-123",
            "testuser",
            "test@example.com",
            "hash",
            "salt",
            UserRoles.Admin,
            new List<string> { "53236321" },
            100_000,
            0,
            true,
            DateTimeOffset.UtcNow,
            null);

        var token = _jwtTokenService.GenerateToken(user);
        var (handler, context) = CreateHandler($"Bearer {token}");
        await handler.InitializeAsync(new AuthenticationScheme("Bearer", null, typeof(BearerAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.NotNull(result.Ticket);
        Assert.Equal("testuser", result.Principal.Identity?.Name);
        Assert.Equal("user-123", result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.True(result.Principal.IsInRole(UserRoles.Admin));
    }

    [Fact]
    public async Task Authenticate_WithoutAuthorizationHeader_ReturnsNoResult()
    {
        var (handler, context) = CreateHandler(authHeader: null);
        await handler.InitializeAsync(new AuthenticationScheme("Bearer", null, typeof(BearerAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Digest username=test")]
    [InlineData("CustomToken xyz123")]
    public async Task Authenticate_WithNonBearerScheme_ReturnsNoResult(string nonBearerHeader)
    {
        var (handler, context) = CreateHandler(nonBearerHeader);
        await handler.InitializeAsync(new AuthenticationScheme("Bearer", null, typeof(BearerAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("Bearer ")]
    [InlineData("Bearer    ")]
    [InlineData("Bearer invalid.token.payload")]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.invalid")]
    public async Task Authenticate_WithInvalidOrTamperedToken_ReturnsFail(string invalidHeader)
    {
        var (handler, context) = CreateHandler(invalidHeader);
        await handler.InitializeAsync(new AuthenticationScheme("Bearer", null, typeof(BearerAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Contains("Geçersiz veya süresi dolmuş", result.Failure.Message);
    }

    [Fact]
    public async Task HandleChallengeAsync_Sets401AndJsonContentType()
    {
        var (handler, context) = CreateHandler();
        await handler.InitializeAsync(new AuthenticationScheme("Bearer", null, typeof(BearerAuthenticationHandler)), context);

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("Yetkisiz erişim", body);
    }

    private sealed class StubIdentityAuthService(bool stampIsValid) : IIdentityAuthService
    {
        public Task<IdentityOperationResult<AppUser>> AuthenticateAsync(string usernameOrEmail, string password, string? remoteIpAddress = null, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(IdentityOperationResult<AppUser>.Fail("unused", System.Net.HttpStatusCode.Unauthorized));
        public Task<IdentityOperationResult<AppUser>> RegisterAsync(string username, string email, string password, string? shopId, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(IdentityOperationResult<AppUser>.Fail("unused", System.Net.HttpStatusCode.BadRequest));
        public Task<bool> LogoutAsync(string userId, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task<bool> ValidateSecurityStampAsync(string userId, string? securityStamp, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(stampIsValid && !string.IsNullOrWhiteSpace(securityStamp));
        public void InvalidateSecurityStampCache(string userId) { }
    }

    [Fact]
    public async Task Authenticate_WhenSecurityStampMatches_ReturnsSuccess()
    {
        var user = new AppUser(
            "user-123", "testuser", "test@example.com", "hash", "", UserRoles.Admin,
            new List<string> { "53236321" }, 100_000, 0, true, DateTimeOffset.UtcNow, null,
            SecurityStamp: "valid-stamp-1");

        var token = _jwtTokenService.GenerateToken(user);
        var (handler, context) = CreateHandler($"Bearer {token}", new StubIdentityAuthService(stampIsValid: true));
        await handler.InitializeAsync(new AuthenticationScheme("Bearer", null, typeof(BearerAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Authenticate_WhenSecurityStampRevokedAfterLogout_ReturnsFail()
    {
        var user = new AppUser(
            "user-123", "testuser", "test@example.com", "hash", "", UserRoles.Admin,
            new List<string> { "53236321" }, 100_000, 0, true, DateTimeOffset.UtcNow, null,
            SecurityStamp: "revoked-stamp");

        var token = _jwtTokenService.GenerateToken(user);
        var (handler, context) = CreateHandler($"Bearer {token}", new StubIdentityAuthService(stampIsValid: false));
        await handler.InitializeAsync(new AuthenticationScheme("Bearer", null, typeof(BearerAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Contains("Oturum sonlandırılmış", result.Failure.Message);
    }
}
