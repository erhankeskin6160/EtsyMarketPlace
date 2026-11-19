namespace EtsyMarketPlace.Api.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Entegrasyon testlerinde PostgreSQL'e bağımlı olmadan SecurityStamp ve Logout iptalini doğrulayan test servisi.</summary>
public sealed class InMemoryTestIdentityAuthService : IIdentityAuthService
{
    public const string DefaultTestUserId = "test-user-id";
    public const string DefaultTestSecurityStamp = "test-security-stamp-001";

    private readonly ConcurrentDictionary<string, string> _stamps = new(StringComparer.Ordinal)
    {
        [DefaultTestUserId] = DefaultTestSecurityStamp
    };

    public void RegisterStamp(string userId, string stamp) => _stamps[userId] = stamp;

    public Task<IdentityOperationResult<AppUser>> AuthenticateAsync(
        string usernameOrEmail,
        string password,
        string? remoteIpAddress = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(IdentityOperationResult<AppUser>.Fail("Geçersiz kullanıcı adı veya şifre.", HttpStatusCode.Unauthorized));

    public Task<IdentityOperationResult<AppUser>> RegisterAsync(
        string username,
        string email,
        string password,
        string? shopId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(IdentityOperationResult<AppUser>.Fail("Test stub.", HttpStatusCode.BadRequest));

    public Task<bool> LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        _stamps[userId] = Guid.NewGuid().ToString("N");
        return Task.FromResult(true);
    }

    public Task<bool> ValidateSecurityStampAsync(string userId, string? securityStamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(securityStamp))
            return Task.FromResult(false);

        return Task.FromResult(_stamps.TryGetValue(userId, out var current) && string.Equals(current, securityStamp, StringComparison.Ordinal));
    }

    public void InvalidateSecurityStampCache(string userId) { }
}

/// <summary>API testleri için deterministik JWT anahtarı ve API anahtarı sağlayan fabrika.</summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "Integration_Test_Secret_Key_0123456789abcdef";
    public const string TestApiKey = "test-api-key-0123456789abcd";

    public InMemoryTestIdentityAuthService TestIdentityService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Security:ApiKey", TestApiKey);
        builder.UseSetting("HealthChecks:BypassDatabaseProbe", "true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIdentityAuthService>();
            services.AddSingleton<IIdentityAuthService>(TestIdentityService);
        });
    }
}

public static class TestAuthHelper
{
    public static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        string userId = InMemoryTestIdentityAuthService.DefaultTestUserId,
        string securityStamp = InMemoryTestIdentityAuthService.DefaultTestSecurityStamp)
    {
        var client = factory.CreateClient();
        var jwt = factory.Services.GetRequiredService<JwtTokenService>();

        if (factory is ApiTestFactory apiFactory)
        {
            apiFactory.TestIdentityService.RegisterStamp(userId, securityStamp);
        }

        var user = new AppUser(
            userId,
            "testadmin",
            "testadmin@local",
            "hash",
            "salt",
            UserRoles.Admin,
            new List<string> { "53236321" },
            1_000_000,
            0,
            true,
            DateTimeOffset.UtcNow,
            null,
            SecurityStamp: securityStamp);

        var token = jwt.GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static WebApplicationFactory<Program> CreateFactoryWithApiKey(string apiKey)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Security:ApiKey", apiKey));
}
