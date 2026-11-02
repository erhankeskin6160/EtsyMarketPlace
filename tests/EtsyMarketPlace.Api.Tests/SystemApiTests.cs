namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class SystemApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SystemApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRoot_ReturnsOkWithServiceInfo()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.Equal("EtsyMarketPlace VDS API", root.GetProperty("service").GetString());
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var status = root.GetProperty("status").GetString();
        Assert.True(status is "Healthy" or "Degraded", $"Beklenmeyen sağlık durumu: {status}");
    }

    [Fact]
    public async Task GetVersion_ReturnsOkWithSystemVersion()
    {
        var response = await _client.GetAsync("/api/system/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal("2.4.0", root.GetProperty("version").GetString());
        Assert.Equal("Online", root.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetSwaggerJson_ReturnsOk()
    {
        // Swagger koruması nedeniyle X-API-KEY veya Basic auth gereklidir
        var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.Add("X-API-KEY", "etsy-vds-sync-key-2026");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
