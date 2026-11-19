namespace EtsyMarketPlace.Application.Tests.MarketResearch;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.MarketResearch;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class EtsyMarketApiClientAuthRetryTests
{
    [Fact]
    public async Task SearchActiveListings_On401ExpiredToken_RefreshesAndRetriesWithFreshBearer()
    {
        var seenAuthHeaders = new List<string?>();
        var handler = new DelegateHttpMessageHandler(req =>
        {
            var auth = req.Headers.Authorization?.ToString();
            seenAuthHeaders.Add(auth);
            if (auth == "Bearer expired-token")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"error\":\"invalid_token\",\"error_description\":\"access token is expired\"}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"count\":1,\"results\":[]}", Encoding.UTF8, "application/json")
            };
        });

        var tokenStore = new InMemoryTokenStore(new EtsyOAuthToken(
            "expired-token", "refresh-123", DateTimeOffset.UtcNow.AddHours(1), "Bearer"));
        var oauth = new StubOAuthService(shouldFail: false);
        var client = new EtsyMarketApiClient(
            new HttpClient(handler),
            tokenStore,
            oauth,
            new StubSettingsRepo(),
            new EmptyConfiguration());

        var json = await client.SearchActiveListingsRawJsonAsync("53236321", "desk lamp", 10, CancellationToken.None);

        Assert.Contains("\"results\"", json);
        Assert.Equal(2, seenAuthHeaders.Count);
        Assert.Equal("Bearer expired-token", seenAuthHeaders[0]);
        Assert.Equal("Bearer fresh-token", seenAuthHeaders[1]);
        Assert.Equal("fresh-token", tokenStore.Current?.AccessToken);
    }

    [Fact]
    public async Task SearchActiveListings_On401AndRefreshFailure_FallsBackToApiKeyOnlyWithoutBearer()
    {
        var seenAuthHeaders = new List<string?>();
        var handler = new DelegateHttpMessageHandler(req =>
        {
            var auth = req.Headers.Authorization?.ToString();
            seenAuthHeaders.Add(auth);
            if (!string.IsNullOrEmpty(auth))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"error\":\"invalid_token\",\"error_description\":\"access token is expired\"}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"count\":2,\"results\":[]}", Encoding.UTF8, "application/json")
            };
        });

        var tokenStore = new InMemoryTokenStore(new EtsyOAuthToken(
            "expired-token", "refresh-123", DateTimeOffset.UtcNow.AddHours(1), "Bearer"));
        var oauth = new StubOAuthService(shouldFail: true);
        var client = new EtsyMarketApiClient(
            new HttpClient(handler),
            tokenStore,
            oauth,
            new StubSettingsRepo(),
            new EmptyConfiguration());

        var json = await client.SearchActiveListingsRawJsonAsync("53236321", "desk lamp", 10, CancellationToken.None);

        Assert.Contains("\"count\":2", json);
        Assert.Equal(2, seenAuthHeaders.Count);
        Assert.Equal("Bearer expired-token", seenAuthHeaders[0]);
        Assert.Null(seenAuthHeaders[1]);
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class InMemoryTokenStore(EtsyOAuthToken? initial) : IEtsyTokenStore
    {
        public EtsyOAuthToken? Current { get; private set; } = initial;
        public Task<EtsyOAuthToken?> GetAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task SaveAsync(string shopId, EtsyOAuthToken token, CancellationToken cancellationToken = default)
        {
            Current = token;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<EtsyConnectedShopDto>> GetConnectedShopsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EtsyConnectedShopDto>>(Array.Empty<EtsyConnectedShopDto>());
    }

    private sealed class StubOAuthService(bool shouldFail) : IEtsyOAuthService
    {
        public Task<string> CreateAuthorizationUrlAsync(string userId, string state, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<EtsyOAuthConnectUrlResult> CreatePkceConnectUrlAsync(string shopId, string? redirectUri = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<EtsyOAuthToken> ExchangeCodeAsync(string shopId, string code, string codeVerifier, string? redirectUri = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<EtsyOAuthToken> ExchangeCodeWithPkceAsync(string shopId, string code, string? state = null, string? codeVerifier = null, string? redirectUri = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<EtsyOAuthToken> RefreshTokenAsync(string shopId, string refreshToken, CancellationToken cancellationToken = default)
        {
            if (shouldFail) throw new InvalidOperationException("Refresh failed");
            return Task.FromResult(new EtsyOAuthToken("fresh-token", "refresh-456", DateTimeOffset.UtcNow.AddHours(1), "Bearer"));
        }
    }

    private sealed class StubSettingsRepo : IShopSettingsRepository
    {
        public Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default)
            => Task.FromResult(("test-keystring", "test-secret", "http://localhost"));

        public Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GetPaymentReserveSnapshotJsonAsync(string shopId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SavePaymentReserveSnapshotJsonAsync(string shopId, string snapshotJson, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class EmptyConfiguration : IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }
}
