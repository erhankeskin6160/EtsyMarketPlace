namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using Xunit;

public sealed class ShopResearchAgentApiTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ShopResearchAgentApiTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDna_Authenticated_ReturnsOkWithShopProfile()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(_factory);

        var response = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("53236321", body);
    }

    [Fact]
    public async Task GetRuns_Authenticated_ReturnsOk()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(_factory);

        var response = await client.GetAsync("/api/etsy/research-agent/runs?shopId=53236321");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDna_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDna_And_Run_AdminWithLegacyTypoAllowedShopIds_ReturnsOk()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Security:AllowedShopIds:0", "523236321"));
        var client = TestAuthHelper.CreateAuthenticatedClient(factory);

        var dnaResponse = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");
        Assert.Equal(HttpStatusCode.OK, dnaResponse.StatusCode);

        var runResponse = await client.PostAsJsonAsync(
            "/api/etsy/research-agent/run?shopId=53236321",
            new { focusHint = (string?)null, maxCandidates = 5, model = (string?)null, apiKey = (string?)null });
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
    }

    [Fact]
    public async Task GetDna_AdminAccessingSecondShop_WithRestrictedAllowedShopIds_ReturnsOk()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Security:AllowedShopIds:0", "53236321"));
        var client = TestAuthHelper.CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/api/etsy/research-agent/dna?shopId=99887766");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("99887766", body);
    }

    [Fact]
    public async Task GetDna_StoreOwner_AllowedForAssignedShop_ForbiddenForUnassignedShop()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(
            _factory,
            userId: "store-owner-user-1",
            securityStamp: "store-owner-stamp-1",
            role: UserRoles.StoreOwner,
            assignedShopIds: ["53236321"]);

        var allowedResponse = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);

        var forbiddenResponse = await client.GetAsync("/api/etsy/research-agent/dna?shopId=99999999");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        var body = await forbiddenResponse.Content.ReadAsStringAsync();
        Assert.Contains("Bu mağaza için erişim yetkiniz bulunmuyor.", body);
    }
}
