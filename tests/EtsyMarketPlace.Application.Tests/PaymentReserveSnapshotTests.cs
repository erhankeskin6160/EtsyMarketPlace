using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Financials;

namespace EtsyMarketPlace.Application.Tests;

public class PaymentReserveSnapshotTests
{
    private sealed class FakeShopSettingsRepository : IShopSettingsRepository
    {
        public string? StoredSnapshotJson { get; set; }
        public int SaveCallCount { get; private set; }

        public Task<string?> GetPaymentReserveSnapshotJsonAsync(string shopId, CancellationToken ct = default) =>
            Task.FromResult(StoredSnapshotJson);

        public Task SavePaymentReserveSnapshotJsonAsync(string shopId, string json, CancellationToken ct = default)
        {
            StoredSnapshotJson = json;
            SaveCallCount++;
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
        public Task<string?> GetAiSettingsJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task SaveAiSettingsJsonAsync(string shopId, string settingsJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GetPaymentReserveSnapshot_ReturnsFallback_WhenDatabaseIsEmpty()
    {
        var fakeRepo = new FakeShopSettingsRepository();
        var handler = new GetPaymentReserveSnapshotHandler(fakeRepo);

        var result = await handler.Handle(new GetPaymentReserveSnapshotQuery("53236321"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var snapshot = (EtsyPaymentReserveSnapshotDto)result.Data;
        Assert.Equal("53236321", snapshot.ShopId);
        Assert.Equal(-50.35m, snapshot.CurrentBalanceTry);
        Assert.Equal(28479.09m, snapshot.PendingBalanceTry);
        Assert.Equal(75m, snapshot.ReservePercent);
        Assert.Equal(60, snapshot.ReserveHoldDays);
        Assert.Equal("November 30", snapshot.ReevaluationDate);
    }

    [Fact]
    public async Task SavePaymentReserveSnapshot_PersistsAndReturnsSuccess()
    {
        var fakeRepo = new FakeShopSettingsRepository();
        var handler = new SavePaymentReserveSnapshotHandler(fakeRepo);
        var dto = new EtsyPaymentReserveSnapshotDto(
            "53236321",
            120.50m,
            15000.00m,
            15120.50m,
            50m,
            45,
            "December 15",
            "50% in reserve for 45 days.",
            DateTimeOffset.UtcNow);

        var result = await handler.Handle(new SavePaymentReserveSnapshotCommand(dto), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var saved = (EtsyPaymentReserveSnapshotDto)result.Data;
        Assert.Equal(50m, saved.ReservePercent);
        Assert.Equal(120.50m, saved.CurrentBalanceTry);
        Assert.Equal(1, fakeRepo.SaveCallCount);
        Assert.NotNull(fakeRepo.StoredSnapshotJson);
        Assert.Contains("120.5", fakeRepo.StoredSnapshotJson);
    }
}
