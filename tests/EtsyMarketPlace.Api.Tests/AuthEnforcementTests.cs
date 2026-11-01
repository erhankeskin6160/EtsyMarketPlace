namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class AuthEnforcementTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AuthEnforcementTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithBearerToken_ReturnsOk()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(_factory);
        var response = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithApiKey_ReturnsOk()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiTestFactory.TestApiKey);

        var response = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithWrongApiKey_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsAnonymous_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
