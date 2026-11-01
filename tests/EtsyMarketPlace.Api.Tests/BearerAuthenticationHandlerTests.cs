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

    private (BearerAuthenticationHandler handler, DefaultHttpContext context) CreateHandler(string? authHeader = null)
    {
        var optionsMonitor = new TestOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        var handler = new BearerAuthenticationHandler(
            optionsMonitor,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            _jwtTokenService);

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
}
