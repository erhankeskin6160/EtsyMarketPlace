namespace EtsyMarketPlace.Api.Tests;

using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Xunit;

public class SwaggerSecurityMiddlewareTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public SwaggerSecurityMiddlewareTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DevelopmentEnvironment_AnonymousRequest_ReturnsSuccess()
    {
        // Development ortamında (varsayılan fabrika ortamı) Swagger anonim erişime izin vermelidir
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Security:Swagger:RequireAuthInDevelopment", "false");
        }).CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("EtsyMarketPlace", content);
    }

    [Fact]
    public async Task Production_AnonymousRequest_Returns401WithBasicAuthHeader()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Security:Swagger:Enabled", "true");
            builder.UseSetting("Security:Swagger:RequireAuth", "true");
            builder.UseSetting("Security:Swagger:Username", "admin");
            builder.UseSetting("Security:Swagger:Password", "secure-pass-2026");
        }).CreateClient();

        // 1. /swagger rotası testi
        var swaggerUiRes = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.Unauthorized, swaggerUiRes.StatusCode);
        Assert.True(swaggerUiRes.Headers.Contains("WWW-Authenticate"));
        Assert.Contains("Basic", swaggerUiRes.Headers.WwwAuthenticate.ToString());

        // 2. /swagger/v1/swagger.json şema dosyası testi
        var swaggerJsonRes = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.Unauthorized, swaggerJsonRes.StatusCode);
        Assert.True(swaggerJsonRes.Headers.Contains("WWW-Authenticate"));
    }

    [Fact]
    public async Task Production_InvalidBasicAuthCredentials_Returns401()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Security:Swagger:Enabled", "true");
            builder.UseSetting("Security:Swagger:RequireAuth", "true");
            builder.UseSetting("Security:Swagger:Username", "admin");
            builder.UseSetting("Security:Swagger:Password", "correct-password");
        }).CreateClient();

        var invalidAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong-password"));
        var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", invalidAuth);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Production_ValidBasicAuthCredentials_Returns200AndSchema()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Security:Swagger:Enabled", "true");
            builder.UseSetting("Security:Swagger:RequireAuth", "true");
            builder.UseSetting("Security:Swagger:Username", "admin");
            builder.UseSetting("Security:Swagger:Password", "correct-password");
        }).CreateClient();

        var validAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:correct-password"));
        var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", validAuth);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("EtsyMarketPlace", body);
    }

    [Fact]
    public async Task Production_WithValidApiKeyHeader_BypassesBasicAuthAndReturns200()
    {
        const string testKey = "api-key-test-bypass-12345";
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Security:ApiKey", testKey);
            builder.UseSetting("Security:Swagger:Enabled", "true");
            builder.UseSetting("Security:Swagger:RequireAuth", "true");
            builder.UseSetting("Security:Swagger:Username", "admin");
            builder.UseSetting("Security:Swagger:Password", "pass-2026");
        }).CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.Add("X-API-KEY", testKey);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Production_WhenSwaggerDisabled_Returns404()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Security:Swagger:Enabled", "false");
        }).CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
