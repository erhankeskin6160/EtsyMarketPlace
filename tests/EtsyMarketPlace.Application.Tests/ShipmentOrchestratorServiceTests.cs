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

    private sealed class FakeNavlungoApiClient : INavlungoApiClient
    {
        public List<NavlungoQuoteOffer> OffersToReturn { get; set; } = new();

        public Task<List<NavlungoQuoteOffer>> FetchLiveQuotesAsync(
            NavlungoQuoteRequest request,
            NavlungoSettings? settings = null,
            CancellationToken cancellationToken = default) => Task.FromResult(OffersToReturn);
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
    public async Task GetCarrierQuotesAsync_PopulatesDetailedBreakdownForShipEntegraAndNavlungoOnly()
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

        var navQuotes = response.Quotes.Where(q => q.CarrierKey == "navlungo").ToList();
        Assert.Equal(4, navQuotes.Count);
        Assert.All(navQuotes, q =>
        {
            Assert.True(q.HasDetailedBreakdown);
            Assert.True(q.HasCustomsBreakdown);
            Assert.True(q.BaseCargoPriceUsd > 0m);
            Assert.True(q.AdditionalFeeUsd > 0m);
            Assert.True(q.FastCustomsPaymentUsd > 0m);
            Assert.Equal(Math.Round(q.PriceUsd + q.FastCustomsPaymentUsd, 2), q.GrandTotalUsd);
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

        var flatCarriers = response.Quotes.Where(q => q.CarrierKey is "shiptomore" or "aras").ToList();
        Assert.NotEmpty(flatCarriers);
        Assert.All(flatCarriers, q =>
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
    public async Task GetCarrierQuotesAsync_MapsOfficialNavlungoQuotesWithExactDdpAndComparesWithShipEntegra()
    {
        var manager = new ShipmentCreationManager();
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();
        var fakeNavlungo = new FakeNavlungoApiClient
        {
            OffersToReturn = new List<NavlungoQuoteOffer>
            {
                new()
                {
                    Carrier = "Widect",
                    CustomerName = "THY Widect Amazon Shipping",
                    ServiceType = "Ekonomi",
                    DeliveryEstimate = "3-7 iş günü",
                    Price = 23.20m,
                    BaseSystemOfferUsd = 21.20m,
                    PeakSeasonSurchargeUsd = 2.00m,
                    DdpFeeUsd = 1.73m,
                    InsuranceFeeUsd = 3.00m,
                    ChargeableWeightKg = 1.6,
                    EarlyTrackingEnabled = true,
                    IsDdpRequired = true,
                    Note = "Navlungo API Canlı Teklif"
                },
                new()
                {
                    Carrier = "FedEx",
                    CustomerName = "Federal Express Express",
                    ServiceType = "Express",
                    DeliveryEstimate = "1-3 iş günü",
                    Price = 25.64m,
                    BaseSystemOfferUsd = 24.64m,
                    PeakSeasonSurchargeUsd = 1.00m,
                    DdpFeeUsd = 4.73m,
                    PremiumTrackingFeeUsd = 5.00m,
                    SignatureRequiredFeeUsd = 5.00m,
                    ChargeableWeightKg = 1.6,
                    EarlyTrackingEnabled = true,
                    IsDdpRequired = true,
                    Note = "Navlungo API Canlı Teklif"
                },
                new()
                {
                    Carrier = "UPS",
                    CustomerName = "United Parcel Service Express",
                    ServiceType = "Express",
                    DeliveryEstimate = "1-3 iş günü",
                    Price = 44.46m,
                    BaseSystemOfferUsd = 28.53m,
                    FuelSurchargeUsd = 14.93m,
                    PeakSeasonSurchargeUsd = 1.00m,
                    DdpFeeUsd = 4.73m,
                    ChargeableWeightKg = 1.6,
                    EarlyTrackingEnabled = true,
                    IsDdpRequired = true,
                    Note = "Navlungo API Canlı Teklif"
                },
                new()
                {
                    Carrier = "UPS",
                    CustomerName = "United Parcel Service Expedited",
                    ServiceType = "Express Saver",
                    DeliveryEstimate = "1-3 iş günü",
                    Price = 48.48m,
                    BaseSystemOfferUsd = 31.16m,
                    FuelSurchargeUsd = 16.32m,
                    PeakSeasonSurchargeUsd = 1.00m,
                    DdpFeeUsd = 4.73m,
                    ChargeableWeightKg = 1.6,
                    EarlyTrackingEnabled = true,
                    IsDdpRequired = true,
                    Note = "Navlungo API Canlı Teklif"
                }
            }
        };

        var orchestrator = new ShipmentOrchestratorService(
            manager,
            fakeLogisticsApi,
            navlungoApiClient: fakeNavlungo);

        var res160 = await orchestrator.GetCarrierQuotesAsync(new CarrierQuotesQuery
        {
            CountryCode = "US",
            PostalCode = "78654",
            WeightKg = 1.0,
            WidthCm = 16,
            LengthCm = 25,
            HeightCm = 20, // (16 * 25 * 20) / 5000 = 1.60 desi
            HsCode = "3926400000",
            Quantity = 1,
            DeclaredValueUsd = new decimalUsdValueOrDefault(1.00m),
            UsdTryRate = 51.91m
        });

        var navWidect = Assert.Single(res160.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-widect");
        Assert.True(navWidect.IsLive);
        Assert.Equal(23.20m, navWidect.PriceUsd);
        Assert.Equal(21.20m, navWidect.BaseCargoPriceUsd);
        Assert.Equal(2.00m, navWidect.AdditionalFeeUsd);
        Assert.Equal(1.73m, navWidect.FastCustomsPaymentUsd);
        Assert.Equal(24.93m, navWidect.GrandTotalUsd);

        var navFedEx = Assert.Single(res160.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-fedex");
        Assert.Equal(25.64m, navFedEx.PriceUsd);
        Assert.Equal(24.64m, navFedEx.BaseCargoPriceUsd);
        Assert.Equal(1.00m, navFedEx.AdditionalFeeUsd);
        Assert.Equal(4.73m, navFedEx.FastCustomsPaymentUsd);
        Assert.Equal(30.37m, navFedEx.GrandTotalUsd);

        var navUpsExp = Assert.Single(res160.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-ups-express");
        Assert.Equal(44.46m, navUpsExp.PriceUsd);
        Assert.Equal(28.53m, navUpsExp.BaseCargoPriceUsd);
        Assert.Equal(14.93m, navUpsExp.FuelCostUsd);
        Assert.Equal(1.00m, navUpsExp.AdditionalFeeUsd);
        Assert.Equal(4.73m, navUpsExp.FastCustomsPaymentUsd);
        Assert.Equal(49.19m, navUpsExp.GrandTotalUsd);

        var navUpsSaver = Assert.Single(res160.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-ups-saver");
        Assert.Equal(48.48m, navUpsSaver.PriceUsd);
        Assert.Equal(31.16m, navUpsSaver.BaseCargoPriceUsd);
        Assert.Equal(16.32m, navUpsSaver.FuelCostUsd);
        Assert.Equal(4.73m, navUpsSaver.FastCustomsPaymentUsd);
        Assert.Equal(53.21m, navUpsSaver.GrandTotalUsd);

        // ShipEntegra 1.60 desi fallback vs Navlungo karşılaştırması
        var seEco = Assert.Single(res160.Quotes, q => q.CarrierKey == "shipentegra" && q.ServiceCode == "shipentegra-amerika-eko-plus");
        Assert.True(seEco.GrandTotalUsd < navWidect.GrandTotalUsd);
        var seUps = Assert.Single(res160.Quotes, q => q.CarrierKey == "shipentegra" && q.ServiceCode == "shipentegra-ups-express");
        Assert.True(navUpsExp.GrandTotalUsd < seUps.GrandTotalUsd);
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

    [Fact]
    public async Task GetCarrierQuotesAsync_ReplacesAnonymousWalkInNavlungoWithCalibratedMemberTariff()
    {
        var manager = new ShipmentCreationManager();
        var fakeLogisticsApi = new FakeEtsyLogisticsApiClient();
        var fakeNavlungo = new FakeNavlungoApiClient
        {
            OffersToReturn = new List<NavlungoQuoteOffer>
            {
                new()
                {
                    Carrier = "Widect",
                    ServiceType = "Ekonomi",
                    Price = 30.07m,
                    DdpFeeUsd = 0m,
                    Note = "Navlungo Canlı Teklif"
                },
                new()
                {
                    Carrier = "FedEx",
                    ServiceType = "Express",
                    Price = 37.03m,
                    DdpFeeUsd = 0m,
                    Note = "Navlungo Canlı Teklif"
                }
            }
        };

        var orchestrator = new ShipmentOrchestratorService(
            manager,
            fakeLogisticsApi,
            navlungoApiClient: fakeNavlungo);

        var res = await orchestrator.GetCarrierQuotesAsync(new CarrierQuotesQuery
        {
            CountryCode = "US",
            PostalCode = "78654",
            WeightKg = 1.0,
            WidthCm = 16,
            LengthCm = 25,
            HeightCm = 20, // 1.60 desi
            HsCode = "3926400000",
            Quantity = 1,
            DeclaredValueUsd = new decimalUsdValueOrDefault(1.00m),
            UsdTryRate = 51.91m
        });

        var navWidect = Assert.Single(res.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-widect");
        Assert.Equal(23.20m, navWidect.PriceUsd);
        Assert.Equal(21.20m, navWidect.BaseCargoPriceUsd);
        Assert.Equal(2.00m, navWidect.AdditionalFeeUsd);
        Assert.Equal(1.68m, navWidect.FastCustomsPaymentUsd);
        Assert.Equal(24.88m, navWidect.GrandTotalUsd);

        var navFedEx = Assert.Single(res.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-fedex");
        Assert.Equal(25.64m, navFedEx.PriceUsd);
        Assert.Equal(4.68m, navFedEx.FastCustomsPaymentUsd);
        Assert.Equal(30.32m, navFedEx.GrandTotalUsd);

        var navUpsExp = Assert.Single(res.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-ups-express");
        Assert.Equal(44.46m, navUpsExp.PriceUsd);
        Assert.Equal(14.93m, navUpsExp.FuelCostUsd);
        Assert.Equal(4.68m, navUpsExp.FastCustomsPaymentUsd);
        Assert.Equal(49.14m, navUpsExp.GrandTotalUsd);

        var navUpsSaver = Assert.Single(res.Quotes, q => q.CarrierKey == "navlungo" && q.ServiceCode == "nav-ups-saver");
        Assert.Equal(48.48m, navUpsSaver.PriceUsd);
        Assert.Equal(16.32m, navUpsSaver.FuelCostUsd);
        Assert.Equal(4.68m, navUpsSaver.FastCustomsPaymentUsd);
        Assert.Equal(53.16m, navUpsSaver.GrandTotalUsd);
    }
}
