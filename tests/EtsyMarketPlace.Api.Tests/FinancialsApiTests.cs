namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class FinancialsApiTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _anonymousClient;

    public FinancialsApiTests(ApiTestFactory factory)
    {
        _anonymousClient = factory.CreateClient();
        _client = TestAuthHelper.CreateAuthenticatedClient(factory);
    }

    [Fact]
    public async Task GetPayouts_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonymousClient.GetAsync("/api/etsy/banking/payouts?shopId=53236321");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPayouts_WhenStartDateIsAfterEndDate_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/etsy/banking/payouts?startDate=2026-12-01&endDate=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetFinancialPerformance_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBankDeposits_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/banking/deposits?shopId=53236321");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
