namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class FinancialsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FinancialsApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
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
