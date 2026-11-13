using System;
using System.IO;
using System.Linq;
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

public sealed class AccountingStatementsApiTests : IClassFixture<ApiTestFactory>, IDisposable
{
    private const string ShopId = "53236321";
    private readonly string _tempDir;
    private readonly WebApplicationFactory<Program> _factory;

    public AccountingStatementsApiTests(ApiTestFactory baseFactory)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"etsy-accounting-api-tests-{Guid.NewGuid():N}");
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

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiTestFactory.TestApiKey);
        return client;
    }

    private static JsonElement UnwrapPayload(JsonDocument doc)
    {
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("success", out var successProp))
        {
            Assert.True(successProp.GetBoolean(), $"API başarısız yanıt döndü: {root}");
        }

        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataProp)
            ? dataProp
            : root;
    }

    [Fact]
    public async Task GetIncomeStatement_WithoutCredentials_ReturnsUnauthorized()
    {
        using var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.GetAsync($"/api/etsy/accounting/income-statement?shopId={ShopId}&period=this_month");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FixedAssets_CrudLifecycle_PersistsViaSqliteFallbackAndReflectedInIncomeStatement()
    {
        using var client = CreateAuthenticatedClient();

        // 1. Ekrandaki gerçek senaryo: "Creality H" (assetAccountCode null gönderilince "253" varsayılanına düşmeli)
        var createPayload = new
        {
            shopId = ShopId,
            assetName = "Creality H",
            assetAccountCode = (string?)null,
            expenseTargetCategory = 632,
            acquisitionDate = "2026-11-13",
            acquisitionCostTry = 22000m,
            salvageValueTry = 0m,
            usefulLifeYears = 5,
            method = 0,
            isActive = true
        };

        var createResp = await client.PostAsJsonAsync($"/api/etsy/accounting/fixed-assets?shopId={ShopId}", createPayload);
        var createBody = await createResp.Content.ReadAsStringAsync();
        Assert.True(createResp.StatusCode == HttpStatusCode.OK || createResp.StatusCode == HttpStatusCode.Created,
            $"Beklenmeyen durum kodu {createResp.StatusCode}: {createBody}");

        long assetId;
        using (var createDoc = JsonDocument.Parse(createBody))
        {
            var payload = UnwrapPayload(createDoc);
            assetId = payload.GetProperty("id").GetInt64();
            Assert.True(assetId > 0);
            Assert.Equal("Creality H", payload.GetProperty("assetName").GetString());
            Assert.Equal("253", payload.GetProperty("assetAccountCode").GetString());
        }

        // 2. GET income-statement içinde kalıcı olarak görünmeli
        var getResp = await client.GetAsync($"/api/etsy/accounting/income-statement?shopId={ShopId}&period=this_month");
        var getBody = await getResp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        using (var getDoc = JsonDocument.Parse(getBody))
        {
            var payload = UnwrapPayload(getDoc);
            var assets = payload.GetProperty("fixedAssets").EnumerateArray().ToList();
            Assert.Contains(assets, a =>
                a.GetProperty("id").GetInt64() == assetId &&
                a.GetProperty("assetName").GetString() == "Creality H" &&
                a.GetProperty("acquisitionCostTry").GetDecimal() == 22000m);
        }

        // 3. PUT güncelleme
        var updatePayload = new
        {
            shopId = ShopId,
            assetName = "Creality H Pro",
            assetAccountCode = "253",
            expenseTargetCategory = 632,
            acquisitionDate = "2026-11-13",
            acquisitionCostTry = 25000m,
            salvageValueTry = 1000m,
            usefulLifeYears = 4,
            method = 0,
            isActive = true
        };

        var putResp = await client.PutAsJsonAsync($"/api/etsy/accounting/fixed-assets/{assetId}?shopId={ShopId}", updatePayload);
        var putBody = await putResp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        using (var putDoc = JsonDocument.Parse(putBody))
        {
            var payload = UnwrapPayload(putDoc);
            Assert.Equal("Creality H Pro", payload.GetProperty("assetName").GetString());
            Assert.Equal(25000m, payload.GetProperty("acquisitionCostTry").GetDecimal());
        }

        // 4. DELETE silme ve gelir tablosundan kalktığını doğrulama
        var delResp = await client.DeleteAsync($"/api/etsy/accounting/fixed-assets/{assetId}?shopId={ShopId}");
        Assert.True(delResp.IsSuccessStatusCode, $"Silme başarısız: {delResp.StatusCode}");

        var afterDelResp = await client.GetAsync($"/api/etsy/accounting/income-statement?shopId={ShopId}&period=this_month");
        Assert.Equal(HttpStatusCode.OK, afterDelResp.StatusCode);
        using var afterDelDoc = JsonDocument.Parse(await afterDelResp.Content.ReadAsStringAsync());
        var remainingAssets = UnwrapPayload(afterDelDoc).GetProperty("fixedAssets").EnumerateArray().ToList();
        Assert.DoesNotContain(remainingAssets, a => a.GetProperty("id").GetInt64() == assetId);
    }

    [Fact]
    public async Task OperatingExpenses_Profile_And_InventoryAdjustment_PersistAndReflectInIncomeStatement()
    {
        using var client = CreateAuthenticatedClient();

        // 1. Profil güncelleme
        var profilePayload = new
        {
            shopId = ShopId,
            commissionTarget = 0,
            shippingTarget = 0,
            canceledCostTarget = 0,
            inventoryMethod = 1,
            taxRegime = 0,
            youngEntrepreneurModeEnabled = true,
            youngEntrepreneurAnnualLimitTry = 400000m,
            youngEntrepreneurPriorUsedTry = 10000m,
            eExportExemptionRatePct = 50m,
            customOrCorporateTaxRatePct = 25m,
            includeFxSpreadAndExchangeDifference = true
        };

        var profileResp = await client.PutAsJsonAsync($"/api/etsy/accounting/profile?shopId={ShopId}", profilePayload);
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);

        // 2. İşletme gideri ekleme (currency null => TRY varsayılanı)
        var expensePayload = new
        {
            shopId = ShopId,
            entryDate = "2026-11-10",
            documentNo = "KIR-01",
            category = 632,
            description = "Atölye Elektrik",
            amount = 1500m,
            currency = (string?)null,
            exchangeRateToTry = 1m,
            isTaxDeductible = true,
            frequency = 0,
            recurringEndDate = (string?)null
        };

        var expCreateResp = await client.PostAsJsonAsync($"/api/etsy/accounting/expenses?shopId={ShopId}", expensePayload);
        var expCreateBody = await expCreateResp.Content.ReadAsStringAsync();
        Assert.True(expCreateResp.StatusCode == HttpStatusCode.OK || expCreateResp.StatusCode == HttpStatusCode.Created,
            $"Beklenmeyen durum kodu {expCreateResp.StatusCode}: {expCreateBody}");

        long expenseId;
        using (var expDoc = JsonDocument.Parse(expCreateBody))
        {
            var payload = UnwrapPayload(expDoc);
            expenseId = payload.GetProperty("id").GetInt64();
            Assert.True(expenseId > 0);
            Assert.Equal("TRY", payload.GetProperty("currency").GetString());
        }

        // 3. Aralıklı envanter dönem sayımı upsert
        var now = DateTime.UtcNow;
        var invPayload = new
        {
            shopId = ShopId,
            year = now.Year,
            month = now.Month,
            openingInventoryTry = 5000m,
            grossPurchasesTry = 3000m,
            purchaseReturnsAndDiscountsTry = 200m,
            closingInventoryTry = 2800m,
            notes = "Kasım sayımı"
        };

        var invResp = await client.PutAsJsonAsync($"/api/etsy/accounting/inventory-adjustment?shopId={ShopId}", invPayload);
        Assert.Equal(HttpStatusCode.OK, invResp.StatusCode);

        // 4. Gelir tablosunda tümünün kalıcı olarak yansıdığını doğrula
        var getResp = await client.GetAsync($"/api/etsy/accounting/income-statement?shopId={ShopId}&period=this_month");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        using (var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync()))
        {
            var payload = UnwrapPayload(doc);
            var statement = payload.GetProperty("statement");
            var appliedProfile = statement.GetProperty("appliedProfile");
            Assert.Equal(1, appliedProfile.GetProperty("inventoryMethod").GetInt32());
            Assert.True(appliedProfile.GetProperty("youngEntrepreneurModeEnabled").GetBoolean());

            var expenses = payload.GetProperty("configuredExpenses").EnumerateArray().ToList();
            Assert.Contains(expenses, e =>
                e.GetProperty("id").GetInt64() == expenseId &&
                e.GetProperty("description").GetString() == "Atölye Elektrik");

            var adjustments = payload.GetProperty("inventoryAdjustments").EnumerateArray().ToList();
            Assert.Contains(adjustments, a =>
                a.GetProperty("year").GetInt32() == now.Year &&
                a.GetProperty("month").GetInt32() == now.Month &&
                a.GetProperty("closingInventoryTry").GetDecimal() == 2800m);
        }

        // 5. Gider güncelle + sil
        var expUpdateResp = await client.PutAsJsonAsync($"/api/etsy/accounting/expenses/{expenseId}?shopId={ShopId}", new
        {
            shopId = ShopId,
            entryDate = "2026-11-10",
            documentNo = "KIR-02",
            category = 632,
            description = "Atölye Elektrik (Düzeltme)",
            amount = 1750m,
            currency = "TRY",
            exchangeRateToTry = 1m,
            isTaxDeductible = true,
            frequency = 0,
            recurringEndDate = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, expUpdateResp.StatusCode);

        var expDeleteResp = await client.DeleteAsync($"/api/etsy/accounting/expenses/{expenseId}?shopId={ShopId}");
        Assert.True(expDeleteResp.IsSuccessStatusCode);
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
            catch
            {
                // Geçici test dizini temizleme hatasını yoksay
            }
        }
    }
}

