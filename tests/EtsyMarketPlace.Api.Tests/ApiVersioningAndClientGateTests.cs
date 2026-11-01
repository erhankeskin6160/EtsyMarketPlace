namespace EtsyMarketPlace.Api.Tests;

using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public class ApiVersioningAndClientGateTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ApiVersioningAndClientGateTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DualRouting_LegacyAndV1Endpoints_BothRespondIdentically()
    {
        var client = _factory.CreateClient();

        // 1. Sürümsüz legacy rota: /health
        var legacyRes = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, legacyRes.StatusCode);

        // 2. Sürümsüz yetkisiz rota: /api/etsy/financial/performance -> 401
        var unversionedAuth = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.Unauthorized, unversionedAuth.StatusCode);

        // 3. V1 rotası: /api/v1/etsy/financial/performance -> 401 (Aynı controller ve action çalışır)
        var versionedAuth = await client.GetAsync("/api/v1/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.Unauthorized, versionedAuth.StatusCode);
    }

    [Fact]
    public async Task DualRouting_AuthenticatedRequest_WorksOnBothPrefixes()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(_factory);

        // Sürümsüz çağrı: /api/etsy/financial/performance
        var unversionedRes = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.OK, unversionedRes.StatusCode);

        // v1 çağrısı: /api/v1/etsy/financial/performance
        var versionedRes = await client.GetAsync("/api/v1/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.OK, versionedRes.StatusCode);
    }

    [Fact]
    public async Task ClientVersionGate_OutdatedExtensionVersion_Returns426UpgradeRequired()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Client-Type", "chrome-extension");
        client.DefaultRequestHeaders.Add("X-Client-Version", "1.0.0"); // Min desteklenen 2.0.0

        var response = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal((HttpStatusCode)426, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal("CLIENT_UPGRADE_REQUIRED", root.GetProperty("error").GetString());
        Assert.Equal("2.0.0", root.GetProperty("minSupportedVersion").GetString());
        Assert.Equal("1.0.0", root.GetProperty("clientVersion").GetString());
    }

    [Fact]
    public async Task ClientVersionGate_CompliantExtensionVersion_AllowsRequestThrough()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Client-Type", "chrome-extension");
        client.DefaultRequestHeaders.Add("X-Client-Version", "2.6.0"); // Desteklenen sürüm

        // Yetkilendirme olmadığı için 426 değil 401 almalı (middleware'den geçtiğini kanıtlar)
        var response = await client.GetAsync("/api/etsy/financial/performance?shopId=53236321&period=last_month");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClientVersionGate_HealthCheck_NeverBlockedEvenWithOldExtension()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Client-Type", "chrome-extension");
        client.DefaultRequestHeaders.Add("X-Client-Version", "0.9.0");

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_GeneratesValidV1Document()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var paths = doc.RootElement.GetProperty("paths");

        // En az 10 uç nokta tanımlı olmalıdır
        Assert.True(paths.EnumerateObject().Count() > 10);
    }
}
