namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Ship to More sağlayıcısının sipariş → API isteği eşleştirmesi ve
/// eksik veri/kimlik durumlarındaki davranışı.
/// </summary>
public sealed class ShiptomoreProviderMappingTests
{
    private sealed class StubApi : IShiptomoreOfficialApi
    {
        public bool HasCredentials { get; set; } = true;
        public string BaseUrl { get; set; } = "https://api.test";
        public ShiptomoreShipmentRequest? LastRequest;

        public Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Dictionary<string, List<ShiptomoreHsCode>> HsCodeResults { get; } = new();
        public List<string> HsCodeQueries { get; } = new();

        public Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default)
        {
            HsCodeQueries.Add(query);
            return Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(
                HsCodeResults.TryGetValue(query, out var results) ? results : new List<ShiptomoreHsCode>());
        }

        public Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ShiptomorePriceOption>>(Array.Empty<ShiptomorePriceOption>());

        public Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(new ShiptomoreShipmentResponse
            {
                Id = "slug-1",
                State = "confirmed",
                TrackingNumbers = { "TRK-1" }
            });
        }

        public Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default)
            => Task.FromResult(new ShiptomoreShipmentDetail());

        public Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task CancelShipmentAsync(string slug, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static ShipmentCreationContext Context(bool withContact = true)
    {
        var order = new EtsyOrderFulfillmentItem
        {
            ReceiptId = 4176634453,
            BuyerName = "Inge Neuer",
            BuyerEmail = withContact ? "inge@example.de" : string.Empty,
            Phone = withContact ? "+4900000" : string.Empty,
            StreetAddress = "Conrad-Scholl-Str 2",
            City = "Koblenz",
            PostalCode = "56068",
            CountryCode = "DE",
            IossNumber = "IM3720000224",
            TotalPrice = 11m,
            Items =
            {
                new EtsyOrderItem { Title = "3D Figur", Quantity = 1, Price = 25m, HsCode = "3926400000" }
            }
        };

        return new ShipmentCreationContext
        {
            Order = order,
            WeightKg = 0.4,
            WidthCm = 15,
            LengthCm = 20,
            HeightCm = 10,
            HsCode = "3926400000",
            SelectedSubCarrier = "UPS",
            ServiceType = "Express",
            CargoPrice = 12.85m
        };
    }

    [Fact]
    public void BuildRequest_AlwaysDeclaresSaleAndPutsIossOnCollectId()
    {
        var request = ShiptomoreOfficialShipmentCreationProvider.BuildRequest(Context());

        Assert.Equal("sale", request.ShipmentType);
        Assert.Equal("IM3720000224", request.CollectIdNumber);
        Assert.Equal("ioss", request.CollectIdType);
        Assert.Null(request.ReceiverIdNo);
        Assert.Equal("#4176634453", request.CustomerReference);
        Assert.True(request.IncludeLabels);
        Assert.Equal("thermal", request.LabelFormat);
    }

    [Fact]
    public void BuildRequest_MapsParcelAndProductLines()
    {
        var request = ShiptomoreOfficialShipmentCreationProvider.BuildRequest(Context());

        var parcel = Assert.Single(request.Parcels);
        Assert.Equal(0.4, parcel.Weight, 3);
        Assert.Equal(20, parcel.Length, 3);
        Assert.Equal(15, parcel.Width, 3);
        Assert.Equal(10, parcel.Height, 3);

        var line = Assert.Single(request.ProductLines);
        Assert.Equal("3D Figur", line.Description);
        Assert.Equal("3926400000", line.HsCode);
        Assert.Equal("TR", line.OriginCountryCode);

        Assert.Equal("ups", request.ProviderSlug);
        Assert.Equal("express", request.ServiceSlug);
        Assert.Equal("DE", request.ReceiverCountryCode);
        Assert.Equal("Inge Neuer", request.ReceiverName);
    }

    [Fact]
    public async Task CreateShipment_RejectsOrderWithoutRequiredContactFields()
    {
        var provider = new ShiptomoreOfficialShipmentCreationProvider(new StubApi());

        var result = await provider.CreateShipmentAsync(Context(withContact: false));

        Assert.False(result.IsSuccess);
        Assert.Contains("e-posta", result.ErrorMessage);
        Assert.Contains("telefon", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateShipment_ExplainsWhenCredentialsAreMissing()
    {
        var provider = new ShiptomoreOfficialShipmentCreationProvider(new StubApi { HasCredentials = false });

        var result = await provider.CreateShipmentAsync(Context());

        Assert.False(result.IsSuccess);
        Assert.Contains("Client ID", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateShipment_ReturnsTrackingNumberOnSuccess()
    {
        var api = new StubApi();
        api.HsCodeResults["39264000"] = new List<ShiptomoreHsCode> { new() { Code = "39264000" } };
        var provider = new ShiptomoreOfficialShipmentCreationProvider(api);

        var result = await provider.CreateShipmentAsync(Context());

        Assert.True(result.IsSuccess);
        Assert.Equal("slug-1", result.ShipmentId);
        Assert.Equal("TRK-1", result.TrackingNumber);
        Assert.NotNull(api.LastRequest);
    }

    [Fact]
    public async Task CreateShipment_ResolvesTenDigitGtipToProviderCode()
    {
        var api = new StubApi();
        api.HsCodeResults["39264000"] = new List<ShiptomoreHsCode> { new() { Code = "39264000" } };
        var provider = new ShiptomoreOfficialShipmentCreationProvider(api);

        var result = await provider.CreateShipmentAsync(Context());

        Assert.True(result.IsSuccess);
        var line = Assert.Single(api.LastRequest!.ProductLines);
        Assert.Equal("39264000", line.HsCode);
        Assert.Equal(new[] { "3926400000", "39264000" }, api.HsCodeQueries);
    }

    [Fact]
    public async Task CreateShipment_FailsClearlyWhenGtipIsUnknownToProvider()
    {
        var api = new StubApi();
        var provider = new ShiptomoreOfficialShipmentCreationProvider(api);

        var result = await provider.CreateShipmentAsync(Context());

        Assert.False(result.IsSuccess);
        Assert.Contains("tanımıyor", result.ErrorMessage);
        Assert.Contains("3926400000", result.ErrorMessage);
        Assert.Null(api.LastRequest);
    }

    [Fact]
    public void Provider_ReportsCreationAsSupported()
    {
        var provider = new ShiptomoreOfficialShipmentCreationProvider(new StubApi());

        Assert.Equal("Shiptomore", provider.ProviderName);
        Assert.True(provider.IsCreationSupported);
    }
}
