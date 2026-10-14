namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;
using Xunit;

public sealed class ShipmentOrchestratorServiceTests
{
    private sealed class FakeShipmentCreationProvider : IShipmentCreationProvider
    {
        public string ProviderName => "ShipEntegra";
        public bool IsCreationSupported => true;
        public ShipmentCreationResult ResultToReturn { get; set; } = new()
        {
            IsSuccess = true,
            ShipmentId = "SE-987654",
            TrackingNumber = "1Z9999999999999999",
            LabelUrl = "https://files.shipentegra.com/label-987654.pdf"
        };

        public Task<ShipmentCreationResult> CreateShipmentAsync(ShipmentCreationContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class FakeEtsyLogisticsApiClient : IEtsyLogisticsApiClient
    {
        public bool SubmittedTracking { get; private set; }
        public string? LastTrackingCode { get; private set; }

        public Task<bool> SubmitTrackingAsync(string shopId, long receiptId, string trackingCode, string carrierName, bool sendBcc = false, CancellationToken cancellationToken = default)
        {
            SubmittedTracking = true;
            LastTrackingCode = trackingCode;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt>> GetFulfillmentReceiptsAsync(string shopId, int lookbackDays = 45, bool? wasShipped = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt>>(new List<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt>());

        public Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult("{}");
        public Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("{}");
        public Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("{}");
        public Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult("{}");
    }

    [Fact]
    public async Task CreateShipmentAsync_InvokesCreationManagerAndSubmitsTrackingToEtsy()
    {
        // Arrange
        var fakeProvider = new FakeShipmentCreationProvider();
        var manager = new ShipmentCreationManager(new[] { fakeProvider });
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();

        var orchestrator = new ShipmentOrchestratorService(manager, fakeLogisticsApi);

        var command = new CreateCarrierShipmentCommand
        {
            ReceiptId = 101,
            ShopId = "53236321",
            CarrierKey = "shipentegra",
            ServiceCode = "shipentegra-express",
            ServiceType = "Express",
            SubCarrier = "ups",
            WeightKg = 0.5,
            WidthCm = 15,
            LengthCm = 20,
            HeightCm = 10,
            NotifyEtsy = true,
            RecipientAddress = new EtsyOrderFulfillmentItem
            {
                ReceiptId = 101,
                BuyerName = "Jane Doe",
                StreetAddress = "123 Main St",
                City = "Austin",
                State = "TX",
                PostalCode = "78701",
                CountryCode = "US"
            }
        };

        // Act
        var result = await orchestrator.CreateShipmentAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("1Z9999999999999999", result.TrackingNumber);
        Assert.Equal("SE-987654", result.CarrierShipmentId);
        Assert.True(result.EtsyTrackingSubmitted);
        Assert.True(fakeLogisticsApi.SubmittedTracking);
        Assert.Equal("1Z9999999999999999", fakeLogisticsApi.LastTrackingCode);
    }

    [Fact]
    public async Task CreateShipmentAsync_WhenNotifyEtsyIsFalse_DoesNotSubmitTrackingToEtsy()
    {
        // Arrange
        var fakeProvider = new FakeShipmentCreationProvider();
        var manager = new ShipmentCreationManager(new[] { fakeProvider });
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();

        var orchestrator = new ShipmentOrchestratorService(manager, fakeLogisticsApi);

        var command = new CreateCarrierShipmentCommand
        {
            ReceiptId = 202,
            ShopId = "53236321",
            CarrierKey = "shipentegra",
            ServiceCode = "shipentegra-express",
            ServiceType = "Express",
            SubCarrier = "ups",
            WeightKg = 0.5,
            NotifyEtsy = false,
            RecipientAddress = new EtsyOrderFulfillmentItem
            {
                ReceiptId = 202,
                BuyerName = "Test Buyer",
                StreetAddress = "456 Test Ave",
                City = "Seattle",
                State = "WA",
                PostalCode = "98101",
                CountryCode = "US"
            }
        };

        // Act
        var result = await orchestrator.CreateShipmentAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("1Z9999999999999999", result.TrackingNumber);
        Assert.False(result.EtsyTrackingSubmitted);
        Assert.False(fakeLogisticsApi.SubmittedTracking);
        Assert.Contains("Test modu", result.EtsyTrackingMessage);
    }

    [Fact]
    public async Task ConnectCarrierAsync_SavesShipEntegraSettings()
    {
        // Arrange
        var manager = new ShipmentCreationManager();
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();

        var orchestrator = new ShipmentOrchestratorService(manager, fakeLogisticsApi);

        var command = new ConnectCarrierCommand
        {
            CarrierKey = "shipentegra",
            ApiKey = "test-api-key-1234",
            ApiSecret = "test-api-secret-5678"
        };

        // Act
        var result = await orchestrator.ConnectCarrierAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("shipentegra", result.CarrierKey);
        Assert.True(result.IsConnected);
    }

    private sealed class FakeShiptomoreOfficialApi : IShiptomoreOfficialApi
    {
        public string BaseUrl { get; set; } = "https://dev.shiptomore.com";
        public bool HasCredentials => true;
        public Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(new List<string> { "ups", "fedex", "dhl" });
        public Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(new List<ShiptomoreHsCode>());
        public Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ShiptomorePriceOption>>(new List<ShiptomorePriceOption>());
        public Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default) =>
            Task.FromResult(new ShiptomoreShipmentResponse { Id = "STM-123", TrackingNumbers = new List<string> { "STM-TRACK-999" } });
        public Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default) =>
            Task.FromResult(new ShiptomoreShipmentDetail());
        public Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        public Task CancelShipmentAsync(string slug, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ConnectCarrierAsync_SavesShiptomoreCredentialsAndValidatesProviders()
    {
        // Arrange
        var manager = new ShipmentCreationManager();
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();
        var fakeShiptomoreApi = new FakeShiptomoreOfficialApi();

        var orchestrator = new ShipmentOrchestratorService(
            manager,
            fakeLogisticsApi,
            shiptomoreApi: fakeShiptomoreApi);

        var command = new ConnectCarrierCommand
        {
            CarrierKey = "shiptomore",
            ApiKey = "NhuCndOc1smCm0uBInrLByukAoV5JNRj",
            ApiSecret = "s3cr3t-test"
        };

        // Act
        var result = await orchestrator.ConnectCarrierAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("shiptomore", result.CarrierKey);
        Assert.True(result.IsConnected);
        Assert.Contains("ups", result.Message);
    }
}
