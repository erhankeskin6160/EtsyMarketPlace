namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.AiUsage;
using Xunit;

public sealed class AiBalanceServiceTests
{
    private sealed class FakeShopSettingsRepository : IShopSettingsRepository
    {
        private string? _aiSettingsJson;

        public FakeShopSettingsRepository(string? json = null)
        {
            _aiSettingsJson = json;
        }

        public Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_aiSettingsJson);

        public Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken cancellationToken = default)
        {
            _aiSettingsJson = settingsJson;
            return Task.CompletedTask;
        }

        public Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<TelegramShopSettings?>(null);
        public Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CarrierSessionRecord>>([]);
        public Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<EtsyAppCredentialsRecord?>(null);
        public Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult(("", "", ""));
        public Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SavedListingAuditRecord>>([]);
        public Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void ParseDeepSeekBalanceJson_AccuratelyExtractsToppedUpBalanceAndCalculatesTry()
    {
        // DeepSeek official GET /user/balance sample response
        const string json = """
        {
            "is_available": true,
            "balance_infos": [
                {
                    "currency": "USD",
                    "total_balance": "1.99",
                    "granted_balance": "0.00",
                    "topped_up_balance": "1.99"
                }
            ]
        }
        """;

        var parsed = AiPriceCalculator.ParseDeepSeekBalanceJson(json, "sk-test-12345678");

        Assert.True(parsed.IsAvailable);
        Assert.Equal(1.99m, parsed.TotalBalanceUsd);
        Assert.Equal(1.99m, parsed.ToppedUpBalanceUsd);
        Assert.Equal("USD", parsed.Currency);

        // Exchange rate test (e.g. 1 USD = 49.38 TRY)
        var balanceTry = parsed.TotalBalanceTry(49.38m);
        Assert.Equal(98.27m, balanceTry);
    }

    [Fact]
    public async Task AiBalanceService_ReturnsAllCoreProvidersWithCalculatedBalances()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test-ai-bal-{Guid.NewGuid():N}.db");
        try
        {
            var usageRepo = new SqliteAiUsageRepository(tempDbPath);
            await usageRepo.InitializeAsync();

            var settingsJson = """
            {
                "provider": "DeepSeek",
                "deepSeekApiKey": "sk-mock-key-12345",
                "deepSeekModel": "DeepSeek-V4-Flash",
                "geminiApiKey": "AIzaSyMockKey",
                "geminiModel": "gemini-2.5-flash"
            }
            """;
            var settingsRepo = new FakeShopSettingsRepository(settingsJson);

            using var httpClient = new HttpClient();
            var service = new AiBalanceService(httpClient, settingsRepo, usageRepo);

            var balances = await service.GetProviderBalancesAsync("53236321", exchangeRate: 49.38m);

            Assert.NotNull(balances);
            Assert.Equal(5, balances.Count);

            // DeepSeek should be active provider
            var ds = balances.FirstOrDefault(x => x.Provider == "DeepSeek");
            Assert.NotNull(ds);
            Assert.True(ds.IsActiveProvider);
            Assert.Equal("DeepSeek-V4-Flash", ds.ActiveModel);
            Assert.Equal(1.99m, ds.TotalBalanceUsd);
            Assert.Equal(98.27m, ds.TotalBalanceTry(49.38m));
            Assert.Contains("platform.deepseek.com", ds.TopUpUrl);

            // Gemini should be present
            var gemini = balances.FirstOrDefault(x => x.Provider == "Google Gemini");
            Assert.NotNull(gemini);
            Assert.Equal("gemini-2.5-flash", gemini.ActiveModel);

            // OpenAI should be present
            var openAi = balances.FirstOrDefault(x => x.Provider == "OpenAI");
            Assert.NotNull(openAi);
            Assert.True(openAi.TotalBalanceUsd > 0);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
