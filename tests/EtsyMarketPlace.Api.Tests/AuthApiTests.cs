namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class AuthApiTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _anonymousClient;
    private readonly HttpClient _authenticatedClient;

    public AuthApiTests(ApiTestFactory factory)
    {
        _factory = factory;
        _anonymousClient = factory.CreateClient();
        _authenticatedClient = TestAuthHelper.CreateAuthenticatedClient(factory);
    }

    [Fact]
    public async Task PostLogin_WithEmptyCredentials_ReturnsBadRequest()
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { usernameOrEmail = "", password = "" }),
            Encoding.UTF8,
            "application/json");

        var response = await _anonymousClient.PostAsync("/api/auth/login", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRegister_WithShortPassword_ReturnsBadRequest()
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { username = "testuser", email = "test@example.com", password = "123" }),
            Encoding.UTF8,
            "application/json");

        var response = await _authenticatedClient.PostAsync("/api/auth/register", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRegister_WithoutToken_ReturnsUnauthorized()
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { username = "testuser", email = "test@example.com", password = "1234567" }),
            Encoding.UTF8,
            "application/json");

        var response = await _anonymousClient.PostAsync("/api/auth/register", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthorizationHeader_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostLogout_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostLogout_WithValidBearerToken_RevokesTokenImmediately()
    {
        var logoutClient = TestAuthHelper.CreateAuthenticatedClient(_factory, "logout-e2e-user", "stamp-before-logout");

        var logoutResponse = await logoutClient.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // Aynı token çıkış sonrasında hemen 401 Unauthorized dönmeli
        var meAfterLogout = await logoutClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);
    }
}
