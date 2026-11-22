namespace EtsyMarketPlace.Application.Tests.AiSettings;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiSettings;
using EtsyMarketPlace.Application.Auth;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Settings;
using EtsyMarketPlace.Infrastructure.Persistence.Repositories;
using Xunit;

public sealed class UserAiSettingsTests
{
    private sealed class InMemoryUserAiSettingsRepo : IUserAiSettingsRepository
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.Ordinal);

        public Task<string?> GetUserAiSettingsJsonAsync(string userId, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(userId, out var json);
            return Task.FromResult(json);
        }

        public Task SaveUserAiSettingsJsonAsync(string userId, string settingsJson, CancellationToken cancellationToken = default)
        {
            _store[userId] = settingsJson;
            return Task.CompletedTask;
        }

        public Task DeleteUserAiSettingsAsync(string userId, CancellationToken cancellationToken = default)
        {
            _store.Remove(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubShopSettingsRepo : IShopSettingsRepository
    {
        private readonly Dictionary<string, string> _shopAi = new(StringComparer.Ordinal);

        public Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken cancellationToken = default)
        {
            _shopAi.TryGetValue(shopId, out var json);
            return Task.FromResult(json);
        }

        public Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken cancellationToken = default)
        {
            _shopAi[shopId] = settingsJson;
            return Task.CompletedTask;
        }

        public Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<TelegramShopSettings?>(null);
        public Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CarrierSessionRecord>>([]);
        public Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<EtsyAppCredentialsRecord?>(null);
        public Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult((string.Empty, string.Empty, string.Empty));
        public Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SavedListingAuditRecord>>([]);
        public Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetPaymentReserveSnapshotJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task SavePaymentReserveSnapshotJsonAsync(string shopId, string snapshotJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubCurrentUserAccessor(string? userId, bool isApiClient = false) : ICurrentUserAccessor
    {
        public string? UserId { get; } = userId;
        public string? Username => UserId;
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
        public bool IsApiClient { get; } = isApiClient;
    }

    [Fact]
    public async Task SaveAndGetAiSettings_IsolatesKeysPerUser_AndSupportsMultipleProviders()
    {
        var userRepo = new InMemoryUserAiSettingsRepo();
        var shopRepo = new StubShopSettingsRepo();
        await shopRepo.SaveAiSettingsJsonAsync("53236321", """{"provider":"Gemini","geminiApiKey":"shared-shop-legacy-key"}""");

        var saveHandler = new SaveAiSettingsHandler(userRepo, shopRepo);
        var getHandler = new GetAiSettingsHandler(userRepo, shopRepo);

        var userAJson = """{"provider":"DeepSeek","geminiApiKey":"AIza-user-A","deepSeekApiKey":"sk-user-A"}""";
        var saveRes = await saveHandler.Handle(new SaveAiSettingsCommandRequest("53236321", userAJson, UserId: "user-A"), CancellationToken.None);
        Assert.True(saveRes.Success);

        // User A retrieves both Gemini and DeepSeek keys
        var getUserA = await getHandler.Handle(new GetAiSettingsRequest("53236321", UserId: "user-A"), CancellationToken.None);
        Assert.True(getUserA.Success);
        var aPayload = JsonSerializer.Serialize(getUserA.Data);
        Assert.Contains("AIza-user-A", aPayload);
        Assert.Contains("sk-user-A", aPayload);

        // User B must NOT see User A's keys OR legacy shop_ai_settings keys
        var getUserB = await getHandler.Handle(new GetAiSettingsRequest("53236321", UserId: "user-B"), CancellationToken.None);
        Assert.True(getUserB.Success);
        var bPayload = JsonSerializer.Serialize(getUserB.Data);
        Assert.Contains("\"exists\":false", bPayload);
        Assert.DoesNotContain("AIza-user-A", bPayload);
        Assert.DoesNotContain("shared-shop-legacy-key", bPayload);
    }

    [Fact]
    public async Task GetAndSaveAiSettings_WithoutUserOrApiClient_ReturnsUnauthorized()
    {
        var userRepo = new InMemoryUserAiSettingsRepo();
        var shopRepo = new StubShopSettingsRepo();

        var getHandler = new GetAiSettingsHandler(userRepo, shopRepo);
        var saveHandler = new SaveAiSettingsHandler(userRepo, shopRepo);

        var getRes = await getHandler.Handle(new GetAiSettingsRequest("53236321", UserId: null, IsApiClient: false), CancellationToken.None);
        Assert.False(getRes.Success);
        Assert.Equal((int)HttpStatusCode.Unauthorized, getRes.StatusCode);

        var saveRes = await saveHandler.Handle(new SaveAiSettingsCommandRequest("53236321", """{"provider":"Gemini"}""", UserId: null, IsApiClient: false), CancellationToken.None);
        Assert.False(saveRes.Success);
        Assert.Equal((int)HttpStatusCode.Unauthorized, saveRes.StatusCode);
    }

    [Fact]
    public async Task SaveAiSettings_WithEmptyJson_ReturnsBadRequest()
    {
        var userRepo = new InMemoryUserAiSettingsRepo();
        var shopRepo = new StubShopSettingsRepo();
        var saveHandler = new SaveAiSettingsHandler(userRepo, shopRepo);

        var res = await saveHandler.Handle(new SaveAiSettingsCommandRequest("53236321", "   ", UserId: "user-A"), CancellationToken.None);
        Assert.False(res.Success);
        Assert.Equal((int)HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ApiClient_FallsBackToShopAiSettings_ForBackwardCompatibility()
    {
        var userRepo = new InMemoryUserAiSettingsRepo();
        var shopRepo = new StubShopSettingsRepo();
        await shopRepo.SaveAiSettingsJsonAsync("53236321", """{"provider":"OpenAI","openAiApiKey":"sk-shop-bot"}""");

        var getHandler = new GetAiSettingsHandler(userRepo, shopRepo);
        var res = await getHandler.Handle(new GetAiSettingsRequest("53236321", UserId: "api-client", IsApiClient: true), CancellationToken.None);

        Assert.True(res.Success);
        var payload = JsonSerializer.Serialize(res.Data);
        Assert.Contains("sk-shop-bot", payload);
    }

    [Fact]
    public async Task UserAiSettingsResolver_StrictlyIsolatesJwtUsers_AndFallsBackOnlyForApiClient()
    {
        var userRepo = new InMemoryUserAiSettingsRepo();
        var shopRepo = new StubShopSettingsRepo();
        await userRepo.SaveUserAiSettingsJsonAsync("user-1", """{"provider":"Gemini","geminiApiKey":"AIza-u1"}""");
        await shopRepo.SaveAiSettingsJsonAsync("53236321", """{"provider":"DeepSeek","deepSeekApiKey":"sk-shop"}""");

        // JWT User 1 gets User 1's key
        var resolverU1 = new UserAiSettingsResolver(new StubCurrentUserAccessor("user-1"), userRepo, shopRepo);
        var jsonU1 = await resolverU1.GetEffectiveAiSettingsJsonAsync("53236321");
        Assert.Contains("AIza-u1", jsonU1);

        // JWT User 2 (no key saved) gets null — NEVER falls back to shop_ai_settings
        var resolverU2 = new UserAiSettingsResolver(new StubCurrentUserAccessor("user-2"), userRepo, shopRepo);
        var jsonU2 = await resolverU2.GetEffectiveAiSettingsJsonAsync("53236321");
        Assert.Null(jsonU2);

        // api-client falls back to shop_ai_settings
        var resolverApi = new UserAiSettingsResolver(new StubCurrentUserAccessor("api-client", isApiClient: true), userRepo, shopRepo);
        var jsonApi = await resolverApi.GetEffectiveAiSettingsJsonAsync("53236321");
        Assert.Contains("sk-shop", jsonApi);
    }

    [Theory]
    [InlineData("""{"provider":"DeepSeek","deepSeekApiKey":"sk-123"}""", "DeepSeek")]
    [InlineData("""{"provider":"Evren"}""", "Evren")]
    [InlineData("""{"geminiApiKey":"AIza"}""", "Gemini")]
    [InlineData("not-valid-json", "Gemini")]
    public void ExtractActiveProvider_ParsesProviderSafely(string json, string expected)
    {
        Assert.Equal(expected, EfPostgreSqlUserAiSettingsRepository.ExtractActiveProvider(json));
    }
}
