namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ListingsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ListingsApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCreateFast_WithEmptyTitle_ReturnsBadRequest()
    {
        var payload = new
        {
            title = "",
            description = "Test description",
            price = 10.0,
            quantity = 1
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/etsy/listings", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOptimizeAi_WithFallback_ReturnsOkWithSuccessResult()
    {
        var payload = new
        {
            title = "",
            tags = new[] { "tag1" }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/etsy/listings/123/ai-optimize", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }
}
