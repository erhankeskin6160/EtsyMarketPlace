namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
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

        public Task<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt?> GetFulfillmentReceiptByIdAsync(string shopId, long receiptId, CancellationToken cancellationToken = default) => Task.FromResult<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt?>(null);
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
    public async Task CreateShipmentAsync_WhenRecipientAddressUsesJsonAliases_SuccessfullyBindsAndCreatesShipment()
    {
        // Arrange
        var fakeProvider = new FakeShipmentCreationProvider();
        var manager = new ShipmentCreationManager(new[] { fakeProvider });
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();

        var orchestrator = new ShipmentOrchestratorService(manager, fakeLogisticsApi);

        string jsonPayload = """
        {
            "receiptId": 303,
            "shopId": "53236321",
            "carrierKey": "shipentegra",
            "notifyEtsy": false,
            "recipientAddress": {
                "name": "Alias Buyer",
                "first_line": "789 Alias Rd",
                "second_line": "Suite 2",
                "city": "Denver",
                "state": "CO",
                "zip": "80201",
                "country_iso": "US",
                "phone": "+15559876543",
                "email": "alias@test.com"
            }
        }
        """;

        var command = System.Text.Json.JsonSerializer.Deserialize<CreateCarrierShipmentCommand>(
            jsonPayload,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(command);
        Assert.NotNull(command.RecipientAddress);
        Assert.Equal("Alias Buyer", command.RecipientAddress.BuyerName);
        Assert.Equal("789 Alias Rd", command.RecipientAddress.StreetAddress);
        Assert.Equal("Suite 2", command.RecipientAddress.SecondAddress);
        Assert.Equal("80201", command.RecipientAddress.PostalCode);
        Assert.Equal("US", command.RecipientAddress.CountryCode);
        Assert.Equal("alias@test.com", command.RecipientAddress.BuyerEmail);

        // Act
        var result = await orchestrator.CreateShipmentAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("1Z9999999999999999", result.TrackingNumber);
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
        public List<ShiptomorePriceOption> PriceOptionsToReturn { get; set; } = new();
        public List<ShiptomoreHsCode> HsCodesToReturn { get; set; } = new();
        public Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(new List<string> { "ups", "fedex", "dhl" });
        public Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ShiptomoreHsCode>>(HsCodesToReturn);
        public Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ShiptomorePriceOption>>(PriceOptionsToReturn);
        public Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default) =>
            Task.FromResult(new ShiptomoreShipmentResponse { Id = "STM-123", TrackingNumbers = new List<string> { "STM-TRACK-999" } });
        public Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default) =>
            Task.FromResult(new ShiptomoreShipmentDetail());
        public Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
        public Task CancelShipmentAsync(string slug, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSecretProtector : IShippingSecretProtector
    {
        public string Protect(string? plainText) => string.IsNullOrEmpty(plainText) ? string.Empty : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));
        public string Unprotect(string? cipherText) => string.IsNullOrEmpty(cipherText) ? string.Empty : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
    }

    [Fact]
    public async Task ConnectCarrierAsync_SavesShiptomoreCredentialsAndValidatesProviders()
    {
        var prevProtector = ShippingSecretProtector.Current;
        ShippingSecretProtector.Current = new FakeSecretProtector();
        try
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
        finally
        {
            ShippingSecretProtector.Current = prevProtector;
        }
    }

    [Fact]
    public async Task GetCarrierQuotesAsync_OnlyPopulatesDetailedBreakdownForShipEntegra()
    {
        var manager = new ShipmentCreationManager();
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();
        var fakeShiptomoreApi = new FakeShiptomoreOfficialApi
        {
            PriceOptionsToReturn = new List<ShiptomorePriceOption>
            {
                new()
                {
                    ProviderSlug = "widect",
                    ServiceSlug = "widect_standard",
                    Price = 14.54m,
                    Currency = "USD",
                    BillingWeight = 0.40
                }
            }
        };

        var orchestrator = new ShipmentOrchestratorService(
            manager,
            fakeLogisticsApi,
            shiptomoreApi: fakeShiptomoreApi);

        var response = await orchestrator.GetCarrierQuotesAsync(new CarrierQuotesQuery
        {
            CountryCode = "US",
            PostalCode = "10001",
            WeightKg = 0.4,
            WidthCm = 15,
            LengthCm = 20,
            HeightCm = 10,
            HsCode = "3926909990",
            Quantity = 1,
            DeclaredValueUsd = new decimalUsdValueOrDefault(1.00m),
            UsdTryRate = 50.37m
        });

        Assert.NotEmpty(response.Quotes);

        var seQuotes = response.Quotes.Where(q => q.CarrierKey == "shipentegra").ToList();
        Assert.NotEmpty(seQuotes);
        Assert.All(seQuotes, q =>
        {
            Assert.True(q.HasDetailedBreakdown);
            Assert.True(q.HasCustomsBreakdown);
            Assert.True(q.BaseCargoPriceUsd > 0m);
            Assert.True(q.FuelCostUsd > 0m);
            Assert.True(q.FastCustomsPaymentUsd > 0m);
        });

        var stmQuote = Assert.Single(response.Quotes, q => q.CarrierKey == "shiptomore");
        Assert.False(stmQuote.HasDetailedBreakdown);
        Assert.True(stmQuote.HasCustomsBreakdown);
        Assert.Equal("widect:widect_standard", stmQuote.ServiceCode);
        Assert.Equal(14.54m, stmQuote.PriceUsd);
        Assert.Equal(0.40, stmQuote.BillableWeightKg);
        Assert.Equal(0m, stmQuote.BaseCargoPriceUsd);
        Assert.Equal(0m, stmQuote.FuelCostUsd);
        Assert.Equal(0.45m, stmQuote.CarrierProcessingFeeUsd);
        Assert.True(stmQuote.FastCustomsPaymentUsd > 0.45m);
        Assert.Equal(Math.Round(stmQuote.PriceUsd + stmQuote.FastCustomsPaymentUsd, 2), stmQuote.GrandTotalUsd);

        var otherCarriers = response.Quotes.Where(q => q.CarrierKey != "shipentegra").ToList();
        Assert.NotEmpty(otherCarriers);
        Assert.All(otherCarriers, q =>
        {
            Assert.False(q.HasDetailedBreakdown);
            Assert.True(q.HasCustomsBreakdown);
            Assert.Equal(0m, q.BaseCargoPriceUsd);
            Assert.Equal(0m, q.FuelCostUsd);
            Assert.True(q.CarrierProcessingFeeUsd > 0m);
            Assert.True(q.FastCustomsPaymentUsd > 0m);
            Assert.Equal(Math.Round(q.PriceUsd + q.FastCustomsPaymentUsd, 2), q.GrandTotalUsd);
        });
    }

    [Fact]
    public async Task GetCarrierQuotesAsync_UsesShiptomoreLiveHsTariffAndCarrierDdpFees()
    {
        var manager = new ShipmentCreationManager();
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();
        var fakeShiptomoreApi = new FakeShiptomoreOfficialApi
        {
            HsCodesToReturn = new List<ShiptomoreHsCode>
            {
                new() { Code = "39264000", Description = "Plastic figurines", UsTariffRate = 5.3 }
            },
            PriceOptionsToReturn = new List<ShiptomorePriceOption>
            {
                new() { ProviderSlug = "widect", ServiceSlug = "widect_standard", Price = 14.54m, Currency = "USD", BillingWeight = 0.40 },
                new() { ProviderSlug = "fedex", ServiceSlug = "fedex_express_3day", Price = 19.29m, Currency = "USD", BillingWeight = 0.60 }
            }
        };

        var orchestrator = new ShipmentOrchestratorService(manager, fakeLogisticsApi, shiptomoreApi: fakeShiptomoreApi);

        var response = await orchestrator.GetCarrierQuotesAsync(new CarrierQuotesQuery
        {
            CountryCode = "US",
            PostalCode = "10001",
            WeightKg = 0.4,
            WidthCm = 15,
            LengthCm = 20,
            HeightCm = 10,
            HsCode = "3926400000",
            Quantity = 2,
            DeclaredValueUsd = new decimalUsdValueOrDefault(10.00m),
            UsdTryRate = 50.00m
        });

        var stmWidect = Assert.Single(response.Quotes, q => q.CarrierKey == "shiptomore" && q.ServiceCode == "widect:widect_standard");
        Assert.False(stmWidect.HasDetailedBreakdown);
        Assert.True(stmWidect.HasCustomsBreakdown);
        Assert.Equal("Shiptomore HS API + DDP Tarife", stmWidect.CustomsSourceBadge);
        Assert.Equal(20.00m, stmWidect.DeclaredValueUsd);
        Assert.Equal(5.3m, stmWidect.GeneralDutyRatePercent);
        Assert.Equal(1.06m, stmWidect.GeneralDutyUsd);
        Assert.Equal(2.50m, stmWidect.AdditionalDutyUsd);
        Assert.Equal(0.45m, stmWidect.CarrierProcessingFeeUsd);
        Assert.Null(stmWidect.CarrierProcessingFeeListUsd);
        Assert.Equal(4.01m, stmWidect.FastCustomsPaymentUsd);
        Assert.Equal(18.55m, stmWidect.GrandTotalUsd);

        var stmFedEx = Assert.Single(response.Quotes, q => q.CarrierKey == "shiptomore" && q.ServiceCode == "fedex:fedex_express_3day");
        Assert.False(stmFedEx.HasDetailedBreakdown);
        Assert.True(stmFedEx.HasCustomsBreakdown);
        Assert.Equal(2.50m, stmFedEx.CarrierProcessingFeeUsd);
        Assert.Equal(4.50m, stmFedEx.CarrierProcessingFeeListUsd);
        Assert.Equal(6.06m, stmFedEx.FastCustomsPaymentUsd);
        Assert.Equal(25.35m, stmFedEx.GrandTotalUsd);
    }

    [Theory]
    [InlineData(true, "DDP")]
    [InlineData(false, "DAP")]
    public void ShiptomoreBuildRequest_SetsIncotermFromIsDdp(bool isDdp, string expectedIncoterm)
    {
        var context = new ShipmentCreationContext
        {
            Order = new EtsyOrderFulfillmentItem
            {
                ReceiptId = 12345,
                BuyerName = "Jane Doe",
                StreetAddress = "123 Main St",
                City = "Austin",
                State = "TX",
                PostalCode = "78701",
                CountryCode = "US",
                Phone = "+15551234567"
            },
            SelectedServiceCode = "widect:widect_standard",
            SelectedSubCarrier = "widect",
            HsCode = "3926400000",
            CargoPrice = 14.54m,
            IsDdp = isDdp
        };

        var request = ShiptomoreOfficialShipmentCreationProvider.BuildRequest(context);

        Assert.Equal(expectedIncoterm, request.IncotermCode);
    }
}
