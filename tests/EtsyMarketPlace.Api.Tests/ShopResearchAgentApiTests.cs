namespace EtsyMarketPlace.Api.Tests;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.ShopResearchAgent.Models;
using EtsyMarketPlace.Domain.ShopResearchAgent;
using Xunit;

public sealed class ShopResearchAgentApiTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ShopResearchAgentApiTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDna_Authenticated_ReturnsOkWithShopProfile()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(_factory);

        var response = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("53236321", body);
    }

    [Fact]
    public async Task GetRuns_Authenticated_ReturnsOk()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(_factory);

        var response = await client.GetAsync("/api/etsy/research-agent/runs?shopId=53236321");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDna_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDna_And_Run_AdminWithLegacyTypoAllowedShopIds_ReturnsOk()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Security:AllowedShopIds:0", "523236321"));
        var client = TestAuthHelper.CreateAuthenticatedClient(factory);

        var dnaResponse = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");
        Assert.Equal(HttpStatusCode.OK, dnaResponse.StatusCode);

        var runResponse = await client.PostAsJsonAsync(
            "/api/etsy/research-agent/run?shopId=53236321",
            new { focusHint = (string?)null, maxCandidates = 5, model = (string?)null, apiKey = (string?)null });
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
    }

    [Fact]
    public async Task GetDna_AdminAccessingSecondShop_WithRestrictedAllowedShopIds_ReturnsOk()
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Security:AllowedShopIds:0", "53236321"));
        var client = TestAuthHelper.CreateAuthenticatedClient(factory);

        var response = await client.GetAsync("/api/etsy/research-agent/dna?shopId=99887766");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("99887766", body);
    }

    [Fact]
    public async Task GetDna_StoreOwner_AllowedForAssignedShop_ForbiddenForUnassignedShop()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(
            _factory,
            userId: "store-owner-user-1",
            securityStamp: "store-owner-stamp-1",
            role: UserRoles.StoreOwner,
            assignedShopIds: ["53236321"]);

        var allowedResponse = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);

        var forbiddenResponse = await client.GetAsync("/api/etsy/research-agent/dna?shopId=99999999");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        var body = await forbiddenResponse.Content.ReadAsStringAsync();
        Assert.Contains("Bu mağaza için erişim yetkiniz bulunmuyor.", body);
    }

    [Fact]
    public async Task PutDna_PersistsV3LandedCostFields_AndReturnsUpdatedProfile()
    {
        var client = TestAuthHelper.CreateAuthenticatedClient(
            _factory,
            userId: "v3-dna-test-user",
            securityStamp: "v3-dna-stamp-1");

        var updatePayload = new UpdateShopDnaRequest(
            PrimaryNiche: "3D Baskı & Masaüstü Dekor",
            TargetAudience: "Tasarım ve hediye alıcıları",
            TargetPriceMinUsd: 18m,
            TargetPriceMaxUsd: 68m,
            MinAcceptableNetMarginPct: 28m,
            EstimatedCogsRatio: 0.32m,
            Supports3DPrint: true,
            PreferredMaterials: ["PLA", "PETG"],
            ForbiddenKeywords: ["disney", "marvel"],
            ScheduleCadence: ResearchScheduleCadence.Daily,
            DefaultShippingCostUsd: 11.5m,
            DefaultPackagingCostUsd: 1.25m,
            ReturnReserveRatePct: 5.5m);

        var putResponse = await client.PutAsJsonAsync("/api/etsy/research-agent/dna?shopId=53236321", updatePayload);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/etsy/research-agent/dna?shopId=53236321");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadAsStringAsync();
        Assert.Contains("11.5", body);
        Assert.Contains("1.25", body);
        Assert.Contains("5.5", body);
    }

    [Fact]
    public async Task StreamEndpoint_Unauthenticated_Returns401_AndStoreOwnerUnassignedShop_Returns403()
    {
        var anonClient = _factory.CreateClient();
        var unauth = await anonClient.GetAsync("/api/etsy/research-agent/stream?shopId=53236321");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        var storeOwner = TestAuthHelper.CreateAuthenticatedClient(
            _factory,
            userId: "store-owner-sse-1",
            securityStamp: "store-owner-sse-stamp-1",
            role: UserRoles.StoreOwner,
            assignedShopIds: ["53236321"]);

        var forbidden = await storeOwner.GetAsync("/api/etsy/research-agent/stream?shopId=99999999");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var admin = TestAuthHelper.CreateAuthenticatedClient(_factory);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var streamResp = await admin.GetAsync(
            "/api/etsy/research-agent/stream?shopId=53236321",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);
        Assert.Equal(HttpStatusCode.OK, streamResp.StatusCode);
        Assert.Equal("text/event-stream", streamResp.Content.Headers.ContentType?.MediaType);
    }
}
