using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminAssignSubscription;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdatePaymentGatewaySettings;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.AdminUpdateSubscriptionPlan;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmAdminTestPayment;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.ConfirmCheckoutPayment;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.CreateCheckoutSession;
using EtsyMarketPlace.Application.Features.Subscriptions.Commands.ProcessPaymentCallback;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetMySubscription;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetPaymentGatewaySettings;
using EtsyMarketPlace.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;
using EtsyMarketPlace.Application.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions;
using EtsyMarketPlace.Domain.Subscriptions.Entities;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.Persistence.Repositories;
using EtsyMarketPlace.Infrastructure.Subscriptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Subscriptions;

public sealed class SubscriptionEntitlementAndCheckoutTests : IDisposable
{
    private readonly string _tempDbPath;

    public SubscriptionEntitlementAndCheckoutTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"etsy_sub_test_{Guid.NewGuid():N}.db");
    }

    private async Task<(SqliteSubscriptionStore SubStore, SqliteEtsyIntegrationStore UserStore, JwtTokenService Jwt, ISubscriptionPaymentGateway Gateway, SubscriptionEntitlementService Entitlement)> CreateHarnessAsync()
    {
        var subStore = new SqliteSubscriptionStore(_tempDbPath);
        await subStore.InitializeAsync();

        var userStore = new SqliteEtsyIntegrationStore(
            Options.Create(new EtsyIntegrationOptions { DatabasePath = _tempDbPath }),
            new EphemeralDataProtectionProvider());
        await userStore.InitializeAsync();

        var jwt = new JwtTokenService("Test_Subscription_Jwt_Secret_Key_0123456789!");
        var config = new EmptyTestConfiguration();
        var gateway = new StripeVirtualPosPaymentGateway(
            new HttpClient(),
            config,
            NullLogger<StripeVirtualPosPaymentGateway>.Instance,
            subStore);
        var entitlement = new SubscriptionEntitlementService(subStore, userStore);

        return (subStore, userStore, jwt, gateway, entitlement);
    }

    private static AppUser CreateTestUser(string id, string role, string planId, int quota, int usedTokens = 0, int shopCount = 1)
    {
        var (hash, salt) = PasswordHasher.HashPassword("TestPass123!");
        var shops = Enumerable.Range(1, shopCount).Select(i => $"5323632{i}").ToList();
        return new AppUser(
            id,
            $"user_{id[..Math.Min(6, id.Length)]}",
            $"{id}@etsytest.local",
            hash,
            salt,
            role,
            shops,
            quota,
            usedTokens,
            true,
            DateTimeOffset.UtcNow,
            null,
            planId,
            "Active",
            DateTimeOffset.UtcNow.AddMonths(1));
    }

    [Fact]
    public async Task DefaultPlans_ShouldSeedAllFourTiersInOrder()
    {
        var (subStore, _, _, _, _) = await CreateHarnessAsync();
        var handler = new GetSubscriptionPlansHandler(subStore);

        var resp = await handler.Handle(new GetSubscriptionPlansQuery(), CancellationToken.None);

        Assert.True(resp.Success);
        var plans = Assert.IsAssignableFrom<IReadOnlyList<SubscriptionPlanDto>>(resp.Data);
        Assert.Equal(4, plans.Count);
        Assert.Equal(new[] { "starter", "growth", "pro", "enterprise" }, plans.Select(p => p.Id).ToArray());
        Assert.True(plans.Single(p => p.Id == "pro").IsPopular);
    }

    [Fact]
    public async Task EntitlementService_StarterPlan_LocksGrowthAndProFeaturesAndEnforcesShopLimit()
    {
        var (_, userStore, _, _, entitlement) = await CreateHarnessAsync();
        var starterUser = CreateTestUser("starter1", UserRoles.StoreOwner, SubscriptionPlanCode.Starter, 100_000, usedTokens: 25_000, shopCount: 1);
        await userStore.CreateUserAsync(starterUser);

        var aiAuditCheck = await entitlement.CheckFeatureAccessAsync(starterUser.Id, SubscriptionFeatureKeys.AiAudit);
        var aiStudioCheck = await entitlement.CheckFeatureAccessAsync(starterUser.Id, SubscriptionFeatureKeys.AiStudio);
        var fastCreatorCheck = await entitlement.CheckFeatureAccessAsync(starterUser.Id, SubscriptionFeatureKeys.FastCreator);
        var carrierAliasCheck = await entitlement.CheckFeatureAccessAsync(starterUser.Id, "aras");
        var shopLimitCheck = await entitlement.CheckShopLimitAsync(starterUser.Id, requestedShopCount: 1);

        Assert.False(aiAuditCheck.Allowed);
        Assert.Equal("growth", aiAuditCheck.RequiredPlanId);
        Assert.False(aiStudioCheck.Allowed);
        Assert.Equal("pro", aiStudioCheck.RequiredPlanId);
        Assert.True(fastCreatorCheck.Allowed);
        Assert.False(carrierAliasCheck.Allowed);
        Assert.Equal("shipping_hub", carrierAliasCheck.NormalizedFeatureKey);
        Assert.False(shopLimitCheck.Allowed);
        Assert.Equal(1, shopLimitCheck.MaxShops);
    }

    [Fact]
    public async Task EntitlementService_AdminRole_IsExemptFromPlanAndQuotaLimits()
    {
        var (_, userStore, _, _, entitlement) = await CreateHarnessAsync();
        var adminUser = CreateTestUser("admin_exempt", UserRoles.Admin, SubscriptionPlanCode.Starter, 100_000, usedTokens: 999_999, shopCount: 10);
        await userStore.CreateUserAsync(adminUser);

        var summary = await entitlement.GetEntitlementSummaryAsync(adminUser.Id);
        var mcpCheck = await entitlement.CheckFeatureAccessAsync(adminUser.Id, SubscriptionFeatureKeys.McpApi);
        var shopLimit = await entitlement.CheckShopLimitAsync(adminUser.Id, 5);
        var quotaCheck = await entitlement.CheckAiTokenQuotaAsync(adminUser.Id, 50_000);

        Assert.True(summary.IsAdminExempt);
        Assert.True(mcpCheck.Allowed);
        Assert.True(shopLimit.Allowed);
        Assert.True(quotaCheck.Allowed);
    }

    [Fact]
    public async Task StripeVirtualPosFlow_CreateSessionAndConfirmPayment_UpgradesPlanAndQuota()
    {
        var (subStore, userStore, jwt, gateway, entitlement) = await CreateHarnessAsync();
        var user = CreateTestUser("seller_upgrade", UserRoles.StoreOwner, SubscriptionPlanCode.Starter, 100_000);
        await userStore.CreateUserAsync(user);

        var createHandler = new CreateCheckoutSessionHandler(subStore, userStore, gateway);
        var createResp = await createHandler.Handle(
            new CreateCheckoutSessionCommand(
                user.Id,
                user.Role,
                user.Username,
                user.Email,
                new CreateCheckoutSessionRequest("pro", "Yearly", "USD", "https://app.etsy.local")),
            CancellationToken.None);

        Assert.True(createResp.Success);
        var session = Assert.IsType<CheckoutSessionResponseDto>(createResp.Data);
        Assert.StartsWith("cs_vpos_", session.SessionId);
        Assert.Contains(session.SessionId, session.CheckoutUrl);
        Assert.Equal(950m, session.AmountUsd);

        var confirmHandler = new ConfirmCheckoutPaymentHandler(subStore, userStore, gateway, entitlement, jwt);
        var confirmResp = await confirmHandler.Handle(
            new ConfirmCheckoutPaymentCommand(
                user.Id,
                user.Role,
                user.Username,
                "127.0.0.1",
                new ConfirmVirtualPosPaymentRequest(
                    session.SessionId,
                    "Erhan Keskin",
                    "4242 4242 4242 4242",
                    "12",
                    "2028",
                    "424")),
            CancellationToken.None);

        Assert.True(confirmResp.Success);
        var confirmed = Assert.IsType<ConfirmPaymentResponseDto>(confirmResp.Data);
        Assert.Equal("Paid", confirmed.Transaction.Status);
        Assert.Equal("**** **** **** 4242", confirmed.Transaction.CardMaskedNumber);
        Assert.Equal("pro", confirmed.Subscription.PlanId);
        Assert.Equal(2_500_000, confirmed.Subscription.MonthlyAiTokenQuota);
        Assert.NotNull(confirmed.RefreshedToken);

        var reloadedUser = await userStore.GetByIdAsync(user.Id);
        Assert.NotNull(reloadedUser);
        Assert.Equal("pro", reloadedUser!.SubscriptionPlanId);
        Assert.Equal(2_500_000, reloadedUser.MonthlyAiTokenQuota);

        var aiStudioAfterUpgrade = await entitlement.CheckFeatureAccessAsync(user.Id, SubscriptionFeatureKeys.AiStudio);
        Assert.True(aiStudioAfterUpgrade.Allowed);
    }

    [Fact]
    public async Task StripeVirtualPosFlow_DeclinedCard_Returns402AndKeepsStarterPlan()
    {
        var (subStore, userStore, jwt, gateway, entitlement) = await CreateHarnessAsync();
        var user = CreateTestUser("seller_decline", UserRoles.StoreOwner, SubscriptionPlanCode.Starter, 100_000);
        await userStore.CreateUserAsync(user);

        var createHandler = new CreateCheckoutSessionHandler(subStore, userStore, gateway);
        var createResp = await createHandler.Handle(
            new CreateCheckoutSessionCommand(
                user.Id,
                user.Role,
                user.Username,
                user.Email,
                new CreateCheckoutSessionRequest("growth", "Monthly", "TRY")),
            CancellationToken.None);
        var session = Assert.IsType<CheckoutSessionResponseDto>(createResp.Data);

        var confirmHandler = new ConfirmCheckoutPaymentHandler(subStore, userStore, gateway, entitlement, jwt);
        var confirmResp = await confirmHandler.Handle(
            new ConfirmCheckoutPaymentCommand(
                user.Id,
                user.Role,
                user.Username,
                "127.0.0.1",
                new ConfirmVirtualPosPaymentRequest(
                    session.SessionId,
                    "Test User",
                    "4000 0000 0000 0002",
                    "12",
                    "2028",
                    "123")),
            CancellationToken.None);

        Assert.False(confirmResp.Success);
        Assert.Equal((int)HttpStatusCode.PaymentRequired, confirmResp.StatusCode);

        var tx = await subStore.GetTransactionBySessionIdAsync(session.SessionId);
        Assert.NotNull(tx);
        Assert.Equal("Failed", tx!.Status);

        var summary = await entitlement.GetEntitlementSummaryAsync(user.Id);
        Assert.Equal("starter", summary.PlanId);
    }

    [Fact]
    public async Task AdminCommands_AssignSubscriptionAndUpdatePlan_PersistChanges()
    {
        var (subStore, userStore, _, _, entitlement) = await CreateHarnessAsync();
        var user = CreateTestUser("seller_admin_assign", UserRoles.StoreOwner, SubscriptionPlanCode.Starter, 100_000);
        await userStore.CreateUserAsync(user);

        var assignHandler = new AdminAssignSubscriptionHandler(subStore, userStore, entitlement);
        var assignResp = await assignHandler.Handle(
            new AdminAssignSubscriptionCommand(
                user.Id,
                "admin-1",
                "Admin",
                "127.0.0.1",
                new AdminAssignSubscriptionRequest("enterprise", "Yearly", "Active", 365, 30, 12_000_000)),
            CancellationToken.None);

        Assert.True(assignResp.Success);
        var summary = Assert.IsType<UserSubscriptionSummaryDto>(assignResp.Data);
        Assert.Equal("enterprise", summary.PlanId);
        Assert.Equal(30, summary.MaxShops);
        Assert.Equal(12_000_000, summary.MonthlyAiTokenQuota);

        var updatePlanHandler = new AdminUpdateSubscriptionPlanHandler(subStore, userStore);
        var updateResp = await updatePlanHandler.Handle(
            new AdminUpdateSubscriptionPlanCommand(
                "growth",
                "admin-1",
                "Admin",
                "127.0.0.1",
                new AdminUpdateSubscriptionPlanRequest(
                    "Growth Plus",
                    "Updated tagline",
                    "POPÜLER BÜYÜME",
                    59m,
                    560m,
                    1990m,
                    19000m,
                    3,
                    900_000,
                    150,
                    20,
                    null,
                    false,
                    true)),
            CancellationToken.None);

        Assert.True(updateResp.Success);
        var updatedPlan = Assert.IsType<SubscriptionPlanDto>(updateResp.Data);
        Assert.Equal("Growth Plus", updatedPlan.Name);
        Assert.Equal(59m, updatedPlan.MonthlyPriceUsd);
        Assert.Equal(3, updatedPlan.MaxShops);

        var myQueryHandler = new GetMySubscriptionHandler(entitlement);
        var myResp = await myQueryHandler.Handle(new GetMySubscriptionQuery(user.Id), CancellationToken.None);
        Assert.True(myResp.Success);
    }

    [Fact]
    public async Task PaymentGatewaySettings_UpdateAndMask_ShouldPersistAndPreserveSecrets()
    {
        var (subStore, userStore, _, gateway, _) = await CreateHarnessAsync();
        var updateHandler = new AdminUpdatePaymentGatewaySettingsHandler(subStore, userStore, gateway);
        var getHandler = new GetPaymentGatewaySettingsHandler(gateway);

        var saveResp = await updateHandler.Handle(
            new AdminUpdatePaymentGatewaySettingsCommand(
                "admin-1",
                "Admin",
                "127.0.0.1",
                new UpdatePaymentGatewaySettingsRequest(
                    ActiveProvider: "PayTR",
                    IsSandbox: true,
                    PaytrMerchantId: "998877",
                    PaytrMerchantKey: "paytr_key_abcd1234",
                    PaytrMerchantSalt: "paytr_salt_xyz9876")),
            CancellationToken.None);

        Assert.True(saveResp.Success);
        var savedDto = Assert.IsType<PaymentGatewaySettingsDto>(saveResp.Data);
        Assert.Equal("PayTR", savedDto.ActiveProvider);
        Assert.True(savedDto.HasPaytrMerchantKey);
        Assert.EndsWith("1234", savedDto.PaytrMerchantKeyMask);
        Assert.DoesNotContain("paytr_key_abcd1234", savedDto.PaytrMerchantKeyMask);

        // Update only IsSandbox without re-entering secrets; existing secrets must remain intact
        var patchResp = await updateHandler.Handle(
            new AdminUpdatePaymentGatewaySettingsCommand(
                "admin-1",
                "Admin",
                "127.0.0.1",
                new UpdatePaymentGatewaySettingsRequest(
                    ActiveProvider: "PayTR",
                    IsSandbox: false)),
            CancellationToken.None);

        Assert.True(patchResp.Success);
        var fetched = await getHandler.Handle(new GetPaymentGatewaySettingsQuery(), CancellationToken.None);
        var fetchedDto = Assert.IsType<PaymentGatewaySettingsDto>(fetched.Data);
        Assert.False(fetchedDto.IsSandbox);
        Assert.True(fetchedDto.HasPaytrMerchantKey);
        Assert.True(fetchedDto.HasPaytrMerchantSalt);
    }

    [Fact]
    public async Task PaytrIframeAndCallbackVerification_ShouldGenerateIframeUrlAndActivateOnValidHmac()
    {
        var (subStore, userStore, jwt, _, entitlement) = await CreateHarnessAsync();
        var user = CreateTestUser("paytr_buyer", UserRoles.StoreOwner, SubscriptionPlanCode.Starter, 100_000);
        await userStore.CreateUserAsync(user);

        const string merchantKey = "test_paytr_key_01";
        const string merchantSalt = "test_paytr_salt_01";
        await subStore.UpsertPaymentGatewayConfigAsync(new PaymentGatewayConfig
        {
            ActiveProvider = "PayTR",
            IsSandbox = true,
            PaytrMerchantId = "123456",
            PaytrMerchantKey = merchantKey,
            PaytrMerchantSalt = merchantSalt
        });

        HttpRequestMessage? capturedReq = null;
        string? capturedForm = null;
        var stubHandler = new StubHttpMessageHandler(async (req, ct) =>
        {
            capturedReq = req;
            capturedForm = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"success\",\"token\":\"paytr_tok_live123\"}", Encoding.UTF8, "application/json")
            };
        });

        var paytrGateway = new StripeVirtualPosPaymentGateway(
            new HttpClient(stubHandler),
            new EmptyTestConfiguration(),
            NullLogger<StripeVirtualPosPaymentGateway>.Instance,
            subStore);

        var createHandler = new CreateCheckoutSessionHandler(subStore, userStore, paytrGateway);
        var sessionResp = await createHandler.Handle(
            new CreateCheckoutSessionCommand(
                user.Id, user.Role, user.Username, user.Email,
                new CreateCheckoutSessionRequest("growth", "Monthly", "TRY", "https://app.example.com"),
                "85.100.10.5"),
            CancellationToken.None);

        Assert.True(sessionResp.Success);
        var session = Assert.IsType<CheckoutSessionResponseDto>(sessionResp.Data);
        Assert.Equal("Iframe", session.Mode);
        Assert.Equal("PayTR", session.Provider);
        Assert.Equal("https://www.paytr.com/odeme/guvenli/paytr_tok_live123", session.CheckoutUrl);
        Assert.DoesNotContain("_", session.SessionId);
        Assert.NotNull(capturedReq);
        Assert.Contains("paytr_token=", capturedForm);

        var callbackHandler = new ProcessPaymentCallbackHandler(subStore, userStore, paytrGateway, entitlement, jwt);

        // 1. Invalid signature must return 400 and leave subscription as Starter
        var badCallback = await callbackHandler.Handle(
            new ProcessPaymentCallbackCommand(
                "PayTR",
                new PaytrCallbackInput(session.SessionId, "success", "169000", "invalid_hash"),
                null,
                "127.0.0.1"),
            CancellationToken.None);

        Assert.False(badCallback.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, badCallback.StatusCode);

        // 2. Valid PayTR HMAC-SHA256 callback activates Growth plan & quota
        var hashPayload = string.Concat(session.SessionId, merchantSalt, "success", "169000");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(merchantKey));
        var validHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(hashPayload)));

        var goodCallback = await callbackHandler.Handle(
            new ProcessPaymentCallbackCommand(
                "PayTR",
                new PaytrCallbackInput(session.SessionId, "success", "169000", validHash),
                null,
                "127.0.0.1"),
            CancellationToken.None);

        Assert.True(goodCallback.Success);
        var upgradedSummary = await entitlement.GetEntitlementSummaryAsync(user.Id);
        Assert.Equal("growth", upgradedSummary.PlanId);
        Assert.Equal(750_000, upgradedSummary.MonthlyAiTokenQuota);
    }

    [Fact]
    public async Task IyzicoCheckoutFormAndAdminTestConfirm_ShouldSignIyzwsV2AndActivate()
    {
        var (subStore, userStore, jwt, unconfiguredGateway, entitlement) = await CreateHarnessAsync();
        var adminUser = CreateTestUser("admin_tester", UserRoles.Admin, SubscriptionPlanCode.Starter, 100_000);
        await userStore.CreateUserAsync(adminUser);

        await subStore.UpsertPaymentGatewayConfigAsync(new PaymentGatewayConfig
        {
            ActiveProvider = "Iyzico",
            IsSandbox = true,
            IyzicoApiKey = "sandbox-iyzi-key",
            IyzicoSecretKey = "sandbox-iyzi-secret",
            IyzicoBaseUrl = "https://sandbox-api.iyzipay.com"
        });

        string? capturedAuthHeader = null;
        var stubHandler = new StubHttpMessageHandler((req, _) =>
        {
            capturedAuthHeader = req.Headers.Authorization?.ToString();
            var url = req.RequestUri?.AbsolutePath ?? string.Empty;
            var json = url.EndsWith("/detail", StringComparison.OrdinalIgnoreCase)
                ? "{\"status\":\"success\",\"paymentStatus\":\"SUCCESS\",\"binNumber\":\"979200\",\"lastFourDigits\":\"0003\"}"
                : "{\"status\":\"success\",\"token\":\"iyz_tok_999\",\"paymentPageUrl\":\"https://sandbox-cpp.iyzipay.com?token=iyz_tok_999\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var iyzicoGateway = new StripeVirtualPosPaymentGateway(
            new HttpClient(stubHandler),
            new EmptyTestConfiguration(),
            NullLogger<StripeVirtualPosPaymentGateway>.Instance,
            subStore);

        var createHandler = new CreateCheckoutSessionHandler(subStore, userStore, iyzicoGateway);
        var initResp = await createHandler.Handle(
            new CreateCheckoutSessionCommand(
                adminUser.Id, adminUser.Role, adminUser.Username, adminUser.Email,
                new CreateCheckoutSessionRequest("pro", "Monthly", "TRY", "https://app.example.com")),
            CancellationToken.None);

        Assert.True(initResp.Success);
        var session = Assert.IsType<CheckoutSessionResponseDto>(initResp.Data);
        Assert.Equal("Iframe", session.Mode);
        Assert.Equal("Iyzico", session.Provider);
        Assert.StartsWith("https://sandbox-cpp.iyzipay.com", session.CheckoutUrl);
        Assert.NotNull(capturedAuthHeader);
        Assert.StartsWith("IYZWSv2 ", capturedAuthHeader);

        // Also test Admin one-click test approval on an unconfigured session
        await subStore.UpsertPaymentGatewayConfigAsync(new PaymentGatewayConfig { ActiveProvider = "Unconfigured" });
        var vposResp = await new CreateCheckoutSessionHandler(subStore, userStore, unconfiguredGateway).Handle(
            new CreateCheckoutSessionCommand(
                adminUser.Id, adminUser.Role, adminUser.Username, adminUser.Email,
                new CreateCheckoutSessionRequest("enterprise", "Monthly", "TRY")),
            CancellationToken.None);
        var vposSession = Assert.IsType<CheckoutSessionResponseDto>(vposResp.Data);
        Assert.Equal("VirtualPos", vposSession.Mode);

        var adminTestResp = await new ConfirmAdminTestPaymentHandler(subStore, userStore, entitlement, jwt).Handle(
            new ConfirmAdminTestPaymentCommand(vposSession.SessionId, adminUser.Id, UserRoles.Admin, adminUser.Username, "127.0.0.1"),
            CancellationToken.None);
        Assert.True(adminTestResp.Success);
        var confirmed = Assert.IsType<ConfirmPaymentResponseDto>(adminTestResp.Data);
        Assert.Equal("enterprise", confirmed.Transaction.PlanId);
        Assert.Equal("Yönetici Test Onayı", confirmed.Transaction.CardMaskedNumber);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        }
        catch { }
    }

    private sealed class EmptyTestConfiguration : IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => new DummyChangeToken();
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();

        private sealed class DummyChangeToken : Microsoft.Extensions.Primitives.IChangeToken
        {
            public bool HasChanged => false;
            public bool ActiveChangeCallbacks => false;
            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
