namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Resmî fiyat kaynağının istek şekli ve teklif eşleştirmesi.
/// Anahtar yokken boş liste döndüğü (uydurma fiyat üretilmediği) de doğrulanır.
/// </summary>
public sealed class ShiptomoreOfficialQuoteSourceTests
{
    private sealed class StubApi : IShiptomoreOfficialApi
    {
        public bool HasCredentials { get; set; } = true;
        public string BaseUrl { get; set; } = "https://api.test";
        public ShiptomorePriceRequest? LastPriceRequest;
        public List<ShiptomorePriceOption> Options { get; set; } = new();

        public Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(Array.Empty<ShiptomoreHsCode>());

        public Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default)
        {
            LastPriceRequest = request;
            return Task.FromResult<IReadOnlyList<ShiptomorePriceOption>>(Options);
        }

        public Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentResponse());

        public Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentDetail());

        public Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task CancelShipmentAsync(string slug, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ShiptomoreQuoteRequest Request() => new()
    {
        ToCountry = "de",
        WeightKg = 0.4,
        HeightCm = 10,
        WidthCm = 15,
        LengthCm = 20,
        Quantity = 1
    };

    [Fact]
    public void BuildPriceRequest_NormalisesCountryAndSendsParcel()
    {
        var built = ShiptomoreOfficialQuoteSource.BuildPriceRequest(Request());

        Assert.Equal("DE", built.CountryCode);
        Assert.Equal("custom", built.PackageType);
        var parcel = Assert.Single(built.Parcels!);
        Assert.Equal(0.4, parcel.Weight, 3);
        Assert.Equal(20, parcel.Length, 3);
        Assert.Equal(1, parcel.Qty);
    }

    [Fact]
    public void BuildPriceRequest_FallsBackToSaneDefaultsForEmptyDimensions()
    {
        var built = ShiptomoreOfficialQuoteSource.BuildPriceRequest(new ShiptomoreQuoteRequest { ToCountry = "US" });

        var parcel = Assert.Single(built.Parcels!);
        Assert.Equal(0.4, parcel.Weight, 3);
        Assert.Equal(20, parcel.Length, 3);
        Assert.Equal(15, parcel.Width, 3);
        Assert.Equal(10, parcel.Height, 3);
    }

    [Fact]
    public async Task GetQuotes_UsesOfficialSlugsAndMarksThemAsLive()
    {
        var api = new StubApi
        {
            Options =
            {
                new ShiptomorePriceOption { ProviderSlug = "ups", ServiceSlug = "ups_express_3day", Price = 12.85m, Currency = "USD", BillingWeight = 0.6 }
            }
        };
        var source = new ShiptomoreOfficialQuoteSource(api);

        var offers = await source.GetQuotesAsync(Request());

        var offer = Assert.Single(offers);
        Assert.Equal("ups", offer.Carrier);
        Assert.Equal("ups_express_3day", offer.ServiceName);
        Assert.Equal(12.85m, offer.Price);
        Assert.Equal(0.6, offer.BillableWeight, 3);
        Assert.Contains("canlı", offer.Note);
        Assert.NotNull(api.LastPriceRequest);
    }

    [Fact]
    public async Task GetQuotes_ReturnsNothingWhenCredentialsAreMissing()
    {
        var api = new StubApi { HasCredentials = false };
        var source = new ShiptomoreOfficialQuoteSource(api);

        Assert.False(source.IsAvailable);
        var offers = await source.GetQuotesAsync(Request());

        Assert.Empty(offers);
        Assert.Null(api.LastPriceRequest); // API'ye hiç gidilmedi
    }

    [Fact]
    public async Task GetQuotes_SkipsOptionsWithoutSlugs()
    {
        var api = new StubApi
        {
            Options =
            {
                new ShiptomorePriceOption { ProviderSlug = "", ServiceSlug = "x", Price = 1m },
                new ShiptomorePriceOption { ProviderSlug = "dhl", ServiceSlug = "", Price = 2m },
                new ShiptomorePriceOption { ProviderSlug = "ups", ServiceSlug = "ground", Price = 3m }
            }
        };
        var source = new ShiptomoreOfficialQuoteSource(api);

        var offers = await source.GetQuotesAsync(Request());

        var offer = Assert.Single(offers);
        Assert.Equal("ups", offer.Carrier);
    }
}
