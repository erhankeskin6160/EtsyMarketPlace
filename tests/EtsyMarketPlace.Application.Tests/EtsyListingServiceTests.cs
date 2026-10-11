namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public class EtsyListingServiceTests
{
    private sealed class FakeEtsyListingApiClient : IEtsyListingApiClient
    {
        public string RawJsonToReturn { get; set; } = "{\"results\":[]}";
        public bool UpdateSuccess { get; set; } = true;
        public bool SetStateSuccess { get; set; } = true;

        public Task<(long ListingId, string Url)> CreateDraftListingAsync(string shopId, CreateDraftListingRawRequest raw, CancellationToken ct) =>
            Task.FromResult((123L, "url"));

        public Task<bool> UploadListingImageAsync(string shopId, long listingId, byte[] imageBytes, string fileName, int rank, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<(bool Success, string? ErrorMessage)> UpdateListingInventoryAsync(string shopId, long listingId, EtsyInventoryPayload payload, CancellationToken ct) =>
            Task.FromResult((true, (string?)null));

        public Task<bool> SetListingStateAsync(string shopId, long listingId, string state, CancellationToken ct) =>
            Task.FromResult(SetStateSuccess);

        public Task<long?> GetDefaultShippingProfileIdAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<long?>(null);

        public Task<long?> GetDefaultReadinessStateIdAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<long?>(null);

        public Task<string> GetShopListingsRawJsonAsync(string shopId, int limit, CancellationToken ct) =>
            Task.FromResult(RawJsonToReturn);

        public Task<string?> GetListingImagesRawJsonAsync(long listingId, string shopId, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task<(bool Success, string Body, string? ErrorMessage)> UpdateListingRawAsync(string shopId, string listingId, IReadOnlyDictionary<string, string> formFields, CancellationToken ct) =>
            Task.FromResult((UpdateSuccess, "{}", UpdateSuccess ? null : "Etsy error"));
    }

    private sealed class FakeShopSettingsRepository : IShopSettingsRepository
    {
        public List<SaveListingAuditRecordRequest> SavedAudits { get; } = new();

        public Task<IReadOnlyList<SavedListingAuditRecord>> GetListingAuditsAsync(string shopId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SavedListingAuditRecord>>(new List<SavedListingAuditRecord>());

        public Task SaveListingAuditAsync(SaveListingAuditRecordRequest request, CancellationToken ct)
        {
            SavedAudits.Add(request);
            return Task.CompletedTask;
        }

        public Task<TelegramShopSettings?> GetTelegramSettingsAsync(string shopId, CancellationToken ct) => Task.FromResult<TelegramShopSettings?>(null);
        public Task<TelegramShopSettings> SaveTelegramSettingsAsync(SaveTelegramSettingsRequest request, CancellationToken ct) => Task.FromResult<TelegramShopSettings>(null!);
        public Task<IReadOnlyList<CarrierSessionRecord>> GetCarrierSessionsAsync(string shopId, CancellationToken ct) => Task.FromResult<IReadOnlyList<CarrierSessionRecord>>(Array.Empty<CarrierSessionRecord>());
        public Task<CarrierSessionRecord> SaveCarrierSessionAsync(SaveCarrierSessionRequest request, CancellationToken ct) => Task.FromResult<CarrierSessionRecord>(null!);
        public Task<EtsyAppCredentialsRecord?> GetEtsyAppCredentialsAsync(string shopId, CancellationToken ct) => Task.FromResult<EtsyAppCredentialsRecord?>(null);
        public Task<(string Keystring, string SharedSecret, string RedirectUri)> GetRawEtsyAppCredentialsAsync(string shopId, CancellationToken ct) => Task.FromResult(("test", "test", "http://localhost"));
        public Task<EtsyAppCredentialsRecord> SaveEtsyAppCredentialsAsync(SaveEtsyAppCredentialsRequest request, CancellationToken ct) => Task.FromResult<EtsyAppCredentialsRecord>(null!);
        public Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task GetShopListingsAsync_ParsesRawJson_AndComputesSeoMetrics()
    {
        var rawJson = """
        {
            "results": [
                {
                    "listing_id": 123456789,
                    "title": "Handmade Wooden Chess Set Board Game Luxury Decor",
                    "description": "Very long description that satisfies the length requirement ... Crafted with genuine walnut and maple wood for collectors.",
                    "views": 42,
                    "num_favorers": 15,
                    "quantity": 5,
                    "price": { "amount": 8500, "divisor": 100, "currency_code": "USD" },
                    "tags": ["chess set", "wooden chess", "board game", "chess board", "gift for him",
                             "handmade game", "luxury chess", "wood carving", "vintage game", "custom chess",
                             "tabletop game", "wooden board", "classic chess"],
                    "materials": ["Walnut", "Maple"],
                    "images": [
                        { "url_570xN": "https://i.etsystatic.com/chess1.jpg" }
                    ]
                }
            ]
        }
        """;

        var fakeClient = new FakeEtsyListingApiClient { RawJsonToReturn = rawJson };
        var fakeRepo = new FakeShopSettingsRepository();
        var service = new EtsyListingService(fakeClient, fakeRepo);

        var result = await service.GetShopListingsAsync("53236321", 50);

        Assert.Single(result);
        var item = result[0];
        Assert.Equal("123456789", item.ListingId);
        Assert.Equal("Handmade Wooden Chess Set Board Game Luxury Decor", item.Title);
        Assert.Equal(85.00m, item.Price);
        Assert.Equal("USD", item.Currency);
        Assert.Equal(13, item.Tags.Count);
        Assert.Equal("https://i.etsystatic.com/chess1.jpg", item.PrimaryImageUrl);
        Assert.True(item.SeoScore >= 60);
    }

    [Fact]
    public async Task UpdateListingAsync_ValidatesTitleLength_ThrowsIfExceeds()
    {
        var fakeClient = new FakeEtsyListingApiClient();
        var fakeRepo = new FakeShopSettingsRepository();
        var service = new EtsyListingService(fakeClient, fakeRepo);
        var req = new UpdateListingRequestDto("53236321", new string('A', 150), "Desc", new[] { "tag1" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateListingAsync("53236321", "123", req));
    }

    [Fact]
    public async Task UpdateListingAsync_ValidatesAmpersandCount_ThrowsIfMoreThanOne()
    {
        var fakeClient = new FakeEtsyListingApiClient();
        var fakeRepo = new FakeShopSettingsRepository();
        var service = new EtsyListingService(fakeClient, fakeRepo);
        var req = new UpdateListingRequestDto("53236321", "Chess & Checkers & Backgammon", "Desc", new[] { "tag1" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateListingAsync("53236321", "123", req));
    }

    [Fact]
    public async Task UpdateListingAsync_WhenValid_CallsApiClientAndSavesAudit()
    {
        var fakeClient = new FakeEtsyListingApiClient { UpdateSuccess = true };
        var fakeRepo = new FakeShopSettingsRepository();
        var service = new EtsyListingService(fakeClient, fakeRepo);
        var req = new UpdateListingRequestDto("53236321", "Handmade Chess Set", "Nice description", new[] { "tag1", "tag2" });

        var res = await service.UpdateListingAsync("53236321", "123", req);

        Assert.True(res.Success);
        Assert.Equal("123", res.ListingId);
        Assert.Single(fakeRepo.SavedAudits);
    }

    [Fact]
    public async Task UpdateListingStateAsync_ThrowsIfInvalidState()
    {
        var fakeClient = new FakeEtsyListingApiClient();
        var fakeRepo = new FakeShopSettingsRepository();
        var service = new EtsyListingService(fakeClient, fakeRepo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateListingStateAsync("53236321", 123, "deleted"));
    }

    [Fact]
    public async Task UpdateListingStateAsync_WhenValid_CallsApiClient()
    {
        var fakeClient = new FakeEtsyListingApiClient { SetStateSuccess = true };
        var fakeRepo = new FakeShopSettingsRepository();
        var service = new EtsyListingService(fakeClient, fakeRepo);

        var res = await service.UpdateListingStateAsync("53236321", 123, "active");

        Assert.True(res.Success);
        Assert.Equal("active", res.State);
    }
}
