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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class ApiKeyAuthenticationHandlerTests
{
    private static (ApiKeyAuthenticationHandler handler, DefaultHttpContext context) CreateHandler(
        string? configuredApiKey,
        string? requestApiKeyHeader = null)
    {
        var configBuilder = new ConfigurationBuilder();
        if (configuredApiKey != null)
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey"] = configuredApiKey
            });
        }
        var configuration = configBuilder.Build();

        var optionsMonitor = new TestOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            configuration);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (requestApiKeyHeader != null)
        {
            context.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = requestApiKeyHeader;
        }

        return (handler, context);
    }

    [Fact]
    public async Task Authenticate_WithConfiguredKey_ReturnsSuccess_WithAdminRoleAndClaims()
    {
        var secretKey = "correct-api-key-123456789";
        var (handler, context) = CreateHandler(secretKey, secretKey);
        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Ticket);
        Assert.NotNull(result.Principal);
        Assert.Equal("api-client", result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("api-client", result.Principal.Identity?.Name);
        Assert.True(result.Principal.IsInRole(UserRoles.Admin));
    }

    [Fact]
    public async Task Authenticate_WithWrongKey_ReturnsFail()
    {
        var secretKey = "correct-api-key-123456789";
        var (handler, context) = CreateHandler(secretKey, "wrong-api-key");
        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Contains("Geçersiz API anahtarı", result.Failure.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Authenticate_WhenConfigKeyMissingOrEmpty_ReturnsNoResult_FailClosed(string? emptyConfigKey)
    {
        // Fail-Closed prensibi: Konfigürasyonda anahtar tanımlı değilse, hiçbir istek kabul edilmemeli!
        var (handler, context) = CreateHandler(emptyConfigKey, "any-attempted-key");
        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Authenticate_WhenHeaderMissing_ReturnsNoResult()
    {
        var (handler, context) = CreateHandler("configured-key", requestApiKeyHeader: null);
        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Authenticate_WhenHeaderEmptyOrWhitespace_ReturnsNoResult(string emptyHeader)
    {
        var (handler, context) = CreateHandler("configured-key", requestApiKeyHeader: emptyHeader);
        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.None);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Authenticate_WithOldHardcodedKey_FailsWhenDifferentConfigured()
    {
        // Regresyon Testi: Eski sabit anahtar (backdoor) yapılandırılmış anahtardan farklıysa asla geçmemeli!
        var (handler, context) = CreateHandler("new-secure-key-999888777", "etsy-vds-sync-key-2026");
        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task HandleChallengeAsync_Sets401AndJsonContentType()
    {
        var (handler, context) = CreateHandler("configured-key");
        await handler.InitializeAsync(new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthenticationHandler)), context);

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("Yetkisiz erişim", body);
    }
}

public class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
{
    public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
