namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Gönderi sonrası işlemler: etiket indirme ve iptal.
/// Kullanıcıya dönen metinlerin anlaşılır olması da test edilir.
/// </summary>
public sealed class ShiptomoreShipmentActionsTests
{
    private sealed class StubApi : IShiptomoreOfficialApi
    {
        public bool HasCredentials { get; set; } = true;
        public string BaseUrl { get; set; } = "https://api.test";
        public byte[] LabelBytes { get; set; } = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        public Exception? CancelThrows { get; set; }
        public string? LastLabelFormat;
        public string? CancelledSlug;

        public Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(Array.Empty<ShiptomoreHsCode>());

        public Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ShiptomorePriceOption>>(Array.Empty<ShiptomorePriceOption>());

        public Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentResponse());

        public Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentDetail());

        public Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default)
        {
            LastLabelFormat = labelFormat;
            return Task.FromResult(LabelBytes);
        }

        public Task CancelShipmentAsync(string slug, CancellationToken ct = default)
        {
            if (CancelThrows != null) throw CancelThrows;
            CancelledSlug = slug;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DownloadLabel_DefaultsToThermalAndSuggestsAFileName()
    {
        var api = new StubApi();
        var actions = new ShiptomoreShipmentActions(api);

        var (bytes, fileName) = await actions.DownloadLabelAsync("abc-123");

        Assert.Equal("thermal", api.LastLabelFormat);
        Assert.Equal(api.LabelBytes, bytes);
        Assert.EndsWith(".pdf", fileName);
        Assert.Contains("termal", fileName);
        Assert.Contains("abc-123", fileName);
    }

    [Fact]
    public async Task DownloadLabel_HonoursA4Request()
    {
        var api = new StubApi();
        var actions = new ShiptomoreShipmentActions(api);

        var (_, fileName) = await actions.DownloadLabelAsync("slug", thermal: false);

        Assert.Equal("a4", api.LastLabelFormat);
        Assert.Contains("a4", fileName);
    }

    [Fact]
    public async Task Cancel_ReportsSuccessInPlainTurkish()
    {
        var api = new StubApi();
        var actions = new ShiptomoreShipmentActions(api);

        string message = await actions.CancelAsync("slug-1");

        Assert.Equal("slug-1", api.CancelledSlug);
        Assert.Contains("iptal edildi", message);
    }

    [Fact]
    public async Task Cancel_ExplainsTheMostCommonRefusal()
    {
        var api = new StubApi { CancelThrows = new ShiptomoreApiException("422", 422, Array.Empty<string>()) };
        var actions = new ShiptomoreShipmentActions(api);

        string message = await actions.CancelAsync("slug-2");

        Assert.Contains("iptal edilemedi", message);
        Assert.Contains("taslak", message);
        Assert.Contains("teslim", message);
    }

    [Fact]
    public async Task Cancel_RefusesWhenTheIntegrationIsNotConnected()
    {
        var actions = new ShiptomoreShipmentActions(new StubApi { HasCredentials = false });

        Assert.False(actions.IsConfigured);
        string message = await actions.CancelAsync("slug-3");

        Assert.Contains("bağlantısı kurulmamış", message);
    }
}
