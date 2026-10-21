namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class AuthApiTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _anonymousClient;
    private readonly HttpClient _authenticatedClient;

    public AuthApiTests(ApiTestFactory factory)
    {
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
}
