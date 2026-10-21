namespace EtsyMarketPlace.Api.Tests;

using System.Collections.Generic;
using System.Net.Http.Headers;
using EtsyMarketPlace.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>API testleri için deterministik JWT anahtarı sağlayan fabrika.</summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "Integration_Test_Secret_Key_0123456789abcdef";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
    }
}

public static class TestAuthHelper
{
    public static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var jwt = factory.Services.GetRequiredService<JwtTokenService>();

        var user = new AppUser(
            "test-user-id",
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
            null);

        var token = jwt.GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static WebApplicationFactory<Program> CreateFactoryWithApiKey(string apiKey)
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Security:ApiKey", apiKey));
}
