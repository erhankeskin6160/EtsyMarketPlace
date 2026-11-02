namespace EtsyMarketPlace.Api.Tests;

using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Xunit;

public class RealHealthAndVersionApiTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public RealHealthAndVersionApiTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_WhenPostgreSqlRequiredAndUnreachable_Returns503ServiceUnavailable()
    {
        // Issue #43 gereksinimi: PostgreSQL zorunlu kılındığında ve erişilemediğinde /health 503 ve "Unhealthy" dönmelidir
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("HealthChecks:BypassDatabaseProbe", "false");
            builder.UseSetting("HealthChecks:RequirePostgreSql", "true");
            // Geçersiz/ulaşılamayan bir IP ve port vererek canlı probe başarısızlığını simüle ediyoruz (hızlı timeout ile)
            builder.UseSetting("ConnectionStrings:PostgreSql", "Host=127.0.0.1;Port=54329;Database=unreachable_db;Username=test;Password=test;Timeout=1;CommandTimeout=1");
        }).CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("missingRequired", out var missingRequired));
        Assert.Contains("Database:PostgreSQL", missingRequired.ToString());
    }

    [Fact]
    public async Task HealthEndpoint_WhenPostgresUnreachableInDualMode_Returns200WithDegradedStatus()
    {
        // VDS mimarisi gereksinimi: PostgreSQL henüz kurulu/çalışmıyor olsa bile SQLite yerel veritabanı aktifse
        // API çökmemeli; 200 OK ve 'Degraded' statüsü dönerek VDS deploy'un başarıyla tamamlanmasını sağlamalıdır.
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("HealthChecks:BypassDatabaseProbe", "false");
            builder.UseSetting("HealthChecks:RequirePostgreSql", "false");
            builder.UseSetting("ConnectionStrings:PostgreSql", "Host=127.0.0.1;Port=54329;Database=unreachable_db;Username=test;Password=test;Timeout=1;CommandTimeout=1");
        }).CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Degraded", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("missingOptional", out var missingOptional));
        Assert.Contains("Database:PostgreSQL", missingOptional.ToString());
    }

    [Fact]
    public async Task SystemVersion_ReturnsDynamicCommitShaAndRuntimeDiagnostics()
    {
        // Issue #43 gereksinimi: /api/system/version Git commit SHA ve çalışma zamanı bilgilerini dönmelidir
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/system/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("2.4.0", root.GetProperty("version").GetString());
        Assert.Equal("PostgreSQL", root.GetProperty("database").GetString());
        Assert.Equal("Online", root.GetProperty("status").GetString());

        // Commit SHA ve InformationalVersion kontrolü
        Assert.True(root.TryGetProperty("commitSha", out var commitShaElement));
        var commitSha = commitShaElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(commitSha));

        Assert.True(root.TryGetProperty("informationalVersion", out var infoVerElement));
        var infoVer = infoVerElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(infoVer));

        // Runtime tanı bilgileri kontrolü
        Assert.True(root.TryGetProperty("runtime", out var runtimeElement));
        Assert.False(string.IsNullOrWhiteSpace(runtimeElement.GetProperty("framework").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(runtimeElement.GetProperty("clrVersion").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(runtimeElement.GetProperty("os").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(runtimeElement.GetProperty("architecture").GetString()));
    }

    [Fact]
    public async Task SystemVersionV1_DualRouting_ReturnsIdenticalStructure()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("2.4.0", root.GetProperty("version").GetString());
        Assert.Equal("PostgreSQL", root.GetProperty("database").GetString());
        Assert.True(root.TryGetProperty("commitSha", out _));
        Assert.True(root.TryGetProperty("runtime", out _));
    }
}
