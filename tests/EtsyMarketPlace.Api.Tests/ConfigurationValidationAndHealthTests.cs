namespace EtsyMarketPlace.Api.Tests;

using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Xunit;

public class ConfigurationValidationAndHealthTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ConfigurationValidationAndHealthTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_WhenEtsyApiKeyMissing_ReturnsDegradedStatus()
    {
        // Varsayılan test ortamında Etsy:ApiKey tanımlı değildir; /health Degraded dönmelidir
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();

        Assert.Equal("Degraded", status);
        Assert.True(doc.RootElement.TryGetProperty("missingOptional", out var missingOpt));
        Assert.Contains("Etsy:ApiKey", missingOpt.ToString());
    }

    [Fact]
    public async Task HealthCheck_WhenAllConfigured_ReturnsHealthyStatus()
    {
        // Etsy:ApiKey sağlandığında /health Healthy dönmelidir
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Etsy:ApiKey", "valid-etsy-client-id-12345");
            builder.UseSetting("Etsy:ShopId", "53236321");
            builder.UseSetting("Gemini:ApiKey", "valid-gemini-ai-key");
        }).CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("status").GetString();

        Assert.Equal("Healthy", status);
    }

    [Fact]
    public async Task ConfigStatus_Endpoint_ReturnsFullComponentBreakdown()
    {
        var client = _factory.CreateClient();

        // Dual-routing: Hem unversioned hem v1 rotası test edilir
        var responseUnversioned = await client.GetAsync("/api/system/config-status");
        var responseV1 = await client.GetAsync("/api/v1/system/config-status");

        Assert.Equal(HttpStatusCode.OK, responseUnversioned.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseV1.StatusCode);

        var json = await responseV1.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("database", out _));
        Assert.True(components.TryGetProperty("security", out _));
        Assert.True(components.TryGetProperty("etsy", out _));
        Assert.True(components.TryGetProperty("geminiAi", out _));
    }

    [Fact]
    public void StartupValidation_WhenApiKeyMissing_FailsFastOnStartup()
    {
        // Security:ApiKey boş olduğunda ValidateOnStart devreye girmeli ve host ayağa kalkamamalıdır
        var customFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Security:ApiKey", "");
            });

        var ex = Assert.ThrowsAny<Exception>(() =>
        {
            _ = customFactory.CreateClient();
        });

        // OptionsValidationException veya aggregate wrapper kontrolü
        Assert.True(
            ex is OptionsValidationException || 
            ex.InnerException is OptionsValidationException ||
            ex.Message.Contains("Security:ApiKey") ||
            (ex.InnerException?.Message.Contains("Security:ApiKey") ?? false),
            $"Beklenen OptionsValidationException fırlatılmadı. Alınan hata: {ex.Message}");
    }
}
