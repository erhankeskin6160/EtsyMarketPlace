namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Threading.Tasks;
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
}
