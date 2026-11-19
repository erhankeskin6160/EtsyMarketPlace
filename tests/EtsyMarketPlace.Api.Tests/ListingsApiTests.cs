namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ListingsApiTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;

    public ListingsApiTests(ApiTestFactory factory)
    {
        _client = TestAuthHelper.CreateAuthenticatedClient(factory);
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

    [Fact]
    public async Task PostAiSuggestVariations_WithEmptyTitleAndDescription_ReturnsBadRequest()
    {
        var payload = new
        {
            title = "",
            description = ""
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/etsy/listings/ai-suggest-variations", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAiSuggestVariations_WithDeepSeekWithoutKeyAndStrictMode_DoesNotLeakGeminiKey_ReturnsBadRequest()
    {
        var payload = new
        {
            title = "Ceramic Coffee Mug",
            description = "Handmade 11oz stoneware mug",
            provider = "DeepSeek",
            model = "deepseek-chat",
            apiKey = "",
            strictNeverOffline = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/etsy/listings/ai-suggest-variations")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Gemini-Api-Key", "AIza-fake-gemini-key-should-not-leak");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("DeepSeek", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostAiSuggestVariations_WithOfflineProvider_ReturnsProductAwareSuggestions()
    {
        var payload = new
        {
            title = "Handmade Ceramic Coffee Mug 11oz",
            description = "Microwave safe stoneware coffee cup in Sage Green",
            provider = "Offline",
            strictNeverOffline = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/etsy/listings/ai-suggest-variations", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("usedAi").GetBoolean());

        var variations = root.GetProperty("suggestedVariations");
        Assert.True(variations.GetArrayLength() >= 1);
        Assert.Equal("Capacity", variations[0].GetProperty("name").GetString());

        var serialized = json;
        Assert.DoesNotContain("Small (15 cm)", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rainbow", serialized, StringComparison.OrdinalIgnoreCase);
    }
}
