namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;
using Xunit;

public sealed class ArasGlobalReceiverAddressTests
{
    [Fact]
    public void UsStateHelper_ResolvesValidCodesAndNames()
    {
        var ca = UsStateHelper.ResolveUsOrCaState("CA");
        Assert.Equal("CA", ca.Code);
        Assert.Equal("California", ca.Name);

        var ny = UsStateHelper.ResolveUsOrCaState("New York");
        Assert.Equal("NY", ny.Code);
        Assert.Equal("New York", ny.Name);

        var fl = UsStateHelper.ResolveUsOrCaState("florida");
        Assert.Equal("FL", fl.Code);
        Assert.Equal("Florida", fl.Name);

        Assert.True(UsStateHelper.RequiresState("US"));
        Assert.True(UsStateHelper.RequiresState("USA"));
        Assert.True(UsStateHelper.RequiresState("CA"));
        Assert.False(UsStateHelper.RequiresState("DE"));
        Assert.False(UsStateHelper.RequiresState("TR"));
    }

    [Fact]
    public void EtsyAddressParser_ParsesStandardJson()
    {
        string json = """
        {
            "name": "John Doe",
            "buyer_email": "john@example.com",
            "first_line": "123 Main St",
            "second_line": "Apt 4B",
            "city": "Los Angeles",
            "state": "California",
            "zip": "90001",
            "country_iso": "US"
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var parsed = EtsyAddressParser.Parse(doc.RootElement);

        Assert.Equal("John Doe", parsed.BuyerName);
        Assert.Equal("john@example.com", parsed.BuyerEmail);
        Assert.Equal("123 Main St", parsed.StreetAddress);
        Assert.Equal("Apt 4B", parsed.SecondAddress);
        Assert.Equal("Los Angeles", parsed.City);
        Assert.Equal("CA", parsed.State); // US normalizasyonu ile CA olmalı
        Assert.Equal("90001", parsed.PostalCode);
        Assert.Equal("US", parsed.CountryCode);
    }

    [Fact]
    public void EtsyAddressParser_ParsesFormattedAddress_WhenFieldsMissing()
    {
        string json = """
        {
            "name": "",
            "first_line": "",
            "city": "",
            "formatted_address": "Jane Smith\r\n456 Elm Street\r\nBrooklyn, NY 11201\r\nUnited States"
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var parsed = EtsyAddressParser.Parse(doc.RootElement);

        Assert.Equal("Jane Smith", parsed.BuyerName);
        Assert.Equal("456 Elm Street", parsed.StreetAddress);
        Assert.Equal("Brooklyn", parsed.City);
        Assert.Equal("NY", parsed.State);
        Assert.Equal("11201", parsed.PostalCode);
    }

    [Fact]
    public async Task ArasGlobalShipmentCreationProvider_FailsFast_WhenAddressIsEmpty()
    {
        var mockApi = new MockArasApiClient();
        var provider = new ArasGlobalShipmentCreationProvider(
            mockApi,
            null,
            null,
            () => new ArasGlobalSettings { BearerToken = "dummy.token" });

        var context = new ShipmentCreationContext
        {
            Order = new EtsyOrderFulfillmentItem
            {
                ReceiptId = 5320755349661,
                BuyerName = "Empty Order",
                StreetAddress = "", // boş adres
                City = "",
                PostalCode = "",
                CountryCode = "US"
            }
        };

        var result = await provider.CreateShipmentAsync(context);

        Assert.False(result.IsSuccess);
        Assert.Contains("Sokak Adresi", result.ErrorMessage);
        Assert.False(mockApi.CreateShipmentCalled, "API'ye taslak oluşturma isteği ATILMAMALIDIR.");
    }

    [Fact]
    public async Task ArasGlobalShipmentCreationProvider_FailsFast_WhenUsOrderHasEmptyState()
    {
        var mockApi = new MockArasApiClient();
        var provider = new ArasGlobalShipmentCreationProvider(
            mockApi,
            null,
            null,
            () => new ArasGlobalSettings { BearerToken = "dummy.token" });

        var context = new ShipmentCreationContext
        {
            Order = new EtsyOrderFulfillmentItem
            {
                ReceiptId = 5320755349661,
                BuyerName = "Jane Doe",
                StreetAddress = "123 Ocean Ave",
                City = "Santa Monica",
                PostalCode = "90401",
                CountryCode = "US",
                State = "" // ABD için eyalet boş
            }
        };

        var result = await provider.CreateShipmentAsync(context);

        Assert.False(result.IsSuccess);
        Assert.Contains("Eyalet", result.ErrorMessage);
        Assert.False(mockApi.CreateShipmentCalled);
    }

    [Fact]
    public async Task ArasGlobalShipmentCreationProvider_PopulatesNormalizedState_WhenValid()
    {
        var mockApi = new MockArasApiClient();
        var provider = new ArasGlobalShipmentCreationProvider(
            mockApi,
            null,
            null,
            () => new ArasGlobalSettings { BearerToken = "Bearer " + JwtTokenInspector.CreateSyntheticToken() });

        var context = new ShipmentCreationContext
        {
            Order = new EtsyOrderFulfillmentItem
            {
                ReceiptId = 5320755349661,
                BuyerName = "Jane Doe",
                StreetAddress = "123 Ocean Ave",
                SecondAddress = "Apt 2",
                City = "Santa Monica",
                PostalCode = "90401",
                CountryCode = "US",
                State = "California" // Tam eyalet ismi verildi
            }
        };

        var result = await provider.CreateShipmentAsync(context);

        Assert.True(mockApi.CreateShipmentCalled);
        Assert.NotNull(mockApi.LastRequest);
        Assert.Equal("CA", mockApi.LastRequest.ReceiverAddress.StateCode);
        Assert.Equal("California", mockApi.LastRequest.ReceiverAddress.StateName);
        Assert.Equal("123 Ocean Ave", mockApi.LastRequest.ReceiverAddress.Details);
        Assert.Equal("Apt 2", mockApi.LastRequest.ReceiverAddress.Details2);
    }

    [Fact]
    public void EtsyOrderService_PreservesAddressOverrides_AcrossSyncLiveQueue()
    {
        var service = new EtsyOrderService();
        var initial = new List<EtsyOrderFulfillmentItem>
        {
            new() { ReceiptId = 1001, BuyerName = "Old Name", StreetAddress = "" }
        };
        service.SyncLiveQueue(initial);

        // Kullanıcı adresi düzenledi
        var updated = new EtsyOrderFulfillmentItem
        {
            ReceiptId = 1001,
            BuyerName = "New Name",
            StreetAddress = "Fixed Street 10",
            City = "Berlin",
            PostalCode = "10115",
            CountryCode = "DE"
        };
        service.UpdateOrderAddress(1001, updated);

        // Arka plan senkronu tekrar boş adresli Etsy verisi getirdi
        var syncedFromEtsy = new List<EtsyOrderFulfillmentItem>
        {
            new() { ReceiptId = 1001, BuyerName = "Old Name", StreetAddress = "" }
        };
        service.SyncLiveQueue(syncedFromEtsy);

        var orders = service.GetOrdersAsync().GetAwaiter().GetResult();
        var order = Assert.Single(orders);
        Assert.Equal("New Name", order.BuyerName);
        Assert.Equal("Fixed Street 10", order.StreetAddress);
        Assert.Equal("Berlin", order.City);
    }

    private sealed class MockArasApiClient : IArasGlobalApiClient
    {
        public bool CreateShipmentCalled { get; private set; }
        public ArasCreateShipmentRequest? LastRequest { get; private set; }

        public Task<ArasCreateShipmentResponse> CreateShipmentAsync(ArasCreateShipmentRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            CreateShipmentCalled = true;
            LastRequest = request;
            return Task.FromResult(new ArasCreateShipmentResponse
            {
                IsSuccess = true,
                ShipmentId = "ARAS-TEST-123",
                ReferenceCode = "REF-TEST-123"
            });
        }

        public Task<ArasShipmentSenderAddress?> GetPrimarySenderAddressAsync(string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ArasShipmentSenderAddress?>(new ArasShipmentSenderAddress
            {
                Title = "Sender Test",
                CityName = "Istanbul",
                CountryCode = "TR"
            });
        }

        public Task<List<ArasGlobalQuoteOffer>> FetchLiveQuotesAsync(ArasGlobalQuoteRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ArasGlobalQuoteOffer>());

        public Task<decimal> TranslateCurrencyAsync(decimal price, string entryCurrency, string exitCurrency, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(price);

        public Task<List<ArasGtipSearchResult>> SearchGtipCodeAsync(string keyword, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ArasGtipSearchResult>());

        public Task<ArasAdditionalOptions> GetAdditionalOptionsAsync(string destinationCountry, string provider, decimal orderTotalUsd, string currency, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new ArasAdditionalOptions { DefaultMethod = "DDP" });

        public Task<string> GetAdditionalInformationAsync(string provider, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult("Bilgi");

        public Task<bool> UpdateShipmentAsync(ArasCreateShipmentRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ArasShipmentPriceBreakdown> CalculateShipmentPriceAsync(string shipmentId, string provider, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new ArasShipmentPriceBreakdown { TotalPrice = 15m, Currency = "USD" });

        public Task<string> GetShipmentLegalDocumentAsync(string shipmentId, string docType, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult("doc-123");

        public Task<bool> StartPriceCalculationForShipmentAsync(ArasStartCalculationPayload payload, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<(bool IsConcluded, List<ArasGlobalQuoteOffer> Offers)> PollBasePriceListAsync(string referenceCode, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult((true, new List<ArasGlobalQuoteOffer>()));

        public Task<bool> SendShipmentPriceAsync(string shipmentId, string provider, decimal cargoPrice, string rawBearerToken, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
