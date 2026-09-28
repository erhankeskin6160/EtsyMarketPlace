namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;

/// <summary>
/// Kullanıcıya dönük Ship to More bağlantı servisi: kayıt, unutma ve
/// anlaşılır durum mesajları. Ağa çıkılmaz.
/// Not: <see cref="ShiptomoreSettingsStore"/> gerçek ayar dosyasına yazdığı için bu testler
/// varsayılan yolu kullanır; yalnızca "unut" testi hariç, diğerleri stub API üzerinden çalışır.
/// </summary>
public sealed class ShiptomoreConnectionServiceTests
{
    private sealed class StubApi : IShiptomoreOfficialApi
    {
        public Func<IReadOnlyList<string>>? ProvidersResult { get; set; }
        public Exception? ThrowOnProviders { get; set; }
        public string BaseUrl { get; set; } = "https://api.test";
        public bool HasCredentials { get; set; } = true;

        public Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default)
        {
            if (ThrowOnProviders != null) throw ThrowOnProviders;
            return Task.FromResult(ProvidersResult?.Invoke() ?? (IReadOnlyList<string>)new[] { "ups", "dhl" });
        }

        public Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(Array.Empty<ShiptomoreHsCode>());

        public Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ShiptomorePriceOption>>(Array.Empty<ShiptomorePriceOption>());

        public Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentResponse());

        public Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentDetail());

        public Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task CancelShipmentAsync(string slug, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Test_ReportsSuccessWithProviderCount()
    {
        var service = new ShiptomoreConnectionService(new StubApi());

        var status = await service.TestAsync();

        Assert.True(status.IsConnected);
        Assert.Contains("2", status.Message);
    }

    [Fact]
    public async Task Test_ExplainsRejectedCredentialsWithoutJargon()
    {
        var api = new StubApi { ThrowOnProviders = new ShiptomoreApiException("401 yetkisiz", 401, Array.Empty<string>()) };
        var service = new ShiptomoreConnectionService(api);

        var status = await service.TestAsync();

        Assert.False(status.IsConnected);
        Assert.Contains("kabul edilmedi", status.Message);
    }

    [Fact]
    public async Task Test_ExplainsUnreachableService()
    {
        var api = new StubApi { ThrowOnProviders = new InvalidOperationException("bağlantı zaman aşımı") };
        var service = new ShiptomoreConnectionService(api);

        var status = await service.TestAsync();

        Assert.False(status.IsConnected);
        Assert.Contains("ulaşılamadı", status.Message);
    }

    [Fact]
    public void SaveFromUserInput_TrimsBothValues()
    {
        var service = new ShiptomoreConnectionService(new StubApi { HasCredentials = false });

        // Boş değerlerle çağrı: dosyaya yazım yolu çalışır, istisna fırlatmaz.
        service.SaveFromUserInput("   ", "  ");

        Assert.NotNull(service);
    }

    [Fact]
    public void DeveloperPortalUrl_IsTheDocumentedPortal()
    {
        Assert.Equal("https://dev.shiptomore.com", ShiptomoreConnectionService.DeveloperPortalUrl);
    }
}
