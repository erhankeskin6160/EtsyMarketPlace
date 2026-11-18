using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EtsyMarketPlace.Api.Tests;

public sealed class SubscriptionsApiTests : IClassFixture<ApiTestFactory>, IDisposable
{
    private readonly string _tempDir;
    private readonly WebApplicationFactory<Program> _factory;

    public SubscriptionsApiTests(ApiTestFactory baseFactory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"etsy-sub-api-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var sqlitePath = Path.Combine(_tempDir, "etsy-finance.db");

        _factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("EtsyIntegration:DatabasePath", sqlitePath);
            builder.UseSetting(
                "ConnectionStrings:PostgreSql",
                "Host=127.0.0.1;Port=59999;Database=unreachable_test;Username=u;Password=p;Timeout=1;Command Timeout=1");
        });
    }

    [Fact]
    public async Task GetPlans_IsAnonymous_AndReturnsFourDefaultPlans()
    {
        using var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/subscriptions/plans");
        var body = await resp.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(4, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetMySubscription_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/subscriptions/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CheckoutAndConfirmPayment_WithBearerAdmin_CompletesStripeVirtualPosLifecycle()
    {
        using var client = TestAuthHelper.CreateAuthenticatedClient(_factory);

        var meResp = await client.GetAsync("/api/subscriptions/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        var sessionResp = await client.PostAsJsonAsync("/api/subscriptions/checkout-session", new
        {
            planId = "pro",
            billingCycle = "Monthly",
            currency = "USD",
            returnBaseUrl = "http://localhost:4200"
        });
        var sessionBody = await sessionResp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, sessionResp.StatusCode);

        string sessionId;
        using (var doc = JsonDocument.Parse(sessionBody))
        {
            sessionId = doc.RootElement.GetProperty("sessionId").GetString() ?? string.Empty;
            Assert.StartsWith("cs_vpos_", sessionId);
            Assert.Equal("VirtualPos", doc.RootElement.GetProperty("mode").GetString());
        }

        var confirmResp = await client.PostAsJsonAsync("/api/subscriptions/confirm-payment", new
        {
            sessionId,
            cardHolderName = "Erhan Keskin",
            cardNumber = "4242 4242 4242 4242",
            expiryMonth = "12",
            expiryYear = "2028",
            cvc = "424"
        });
        var confirmBody = await confirmResp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        using (var doc = JsonDocument.Parse(confirmBody))
        {
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Paid", doc.RootElement.GetProperty("transaction").GetProperty("status").GetString());
            Assert.Equal("**** **** **** 4242", doc.RootElement.GetProperty("transaction").GetProperty("cardMaskedNumber").GetString());
        }

        var txResp = await client.GetAsync("/api/subscriptions/transactions");
        Assert.Equal(HttpStatusCode.OK, txResp.StatusCode);
        using var txDoc = JsonDocument.Parse(await txResp.Content.ReadAsStringAsync());
        Assert.True(txDoc.RootElement.GetArrayLength() >= 1);
    }

    public void Dispose()
    {
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch { }
        }
    }
}
