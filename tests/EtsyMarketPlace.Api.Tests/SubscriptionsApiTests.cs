using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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

    [Fact]
    public async Task AdminPaymentSettings_And_ConfirmAdminTest_And_PaytrCallback_WorkEndToEnd()
    {
        using var adminClient = TestAuthHelper.CreateAuthenticatedClient(_factory);

        // 1. Update payment gateway settings with PayTR sandbox values
        var updateResp = await adminClient.PutAsJsonAsync("/api/admin/subscriptions/payment-settings", new
        {
            activeProvider = "PayTR",
            isSandbox = true,
            paytrMerchantId = "776655",
            paytrMerchantKey = "api_paytr_key_9999",
            paytrMerchantSalt = "api_paytr_salt_8888"
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);

        var getResp = await adminClient.GetAsync("/api/admin/subscriptions/payment-settings");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        using (var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PayTR", doc.RootElement.GetProperty("activeProvider").GetString());
            Assert.True(doc.RootElement.GetProperty("hasPaytrMerchantKey").GetBoolean());
            Assert.EndsWith("9999", doc.RootElement.GetProperty("paytrMerchantKeyMask").GetString());
        }

        // 2. Invalid PayTR callback hash returns 400 BadRequest
        using var anonClient = _factory.CreateClient();
        var badCbResp = await anonClient.PostAsync("/api/subscriptions/callback/paytr",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["merchant_oid"] = "PAYTRUNKNOWN0001",
                ["status"] = "success",
                ["total_amount"] = "65000",
                ["hash"] = "bad_hash"
            }));
        Assert.Equal(HttpStatusCode.BadRequest, badCbResp.StatusCode);

        // 3. Switch back to Unconfigured and test Admin one-click test activation endpoint
        var resetResp = await adminClient.PutAsJsonAsync("/api/admin/subscriptions/payment-settings", new
        {
            activeProvider = "Unconfigured",
            isSandbox = true
        });
        Assert.Equal(HttpStatusCode.OK, resetResp.StatusCode);

        var sessionResp = await adminClient.PostAsJsonAsync("/api/subscriptions/checkout-session", new
        {
            planId = "starter",
            billingCycle = "Monthly",
            currency = "TRY"
        });
        Assert.Equal(HttpStatusCode.OK, sessionResp.StatusCode);
        using var sessionDoc = JsonDocument.Parse(await sessionResp.Content.ReadAsStringAsync());
        var sessionId = sessionDoc.RootElement.GetProperty("sessionId").GetString() ?? string.Empty;

        var adminTestResp = await adminClient.PostAsJsonAsync("/api/subscriptions/confirm-admin-test", new { sessionId });
        Assert.Equal(HttpStatusCode.OK, adminTestResp.StatusCode);
        using var testDoc = JsonDocument.Parse(await adminTestResp.Content.ReadAsStringAsync());
        Assert.True(testDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Yönetici Test Onayı", testDoc.RootElement.GetProperty("transaction").GetProperty("cardMaskedNumber").GetString());
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
