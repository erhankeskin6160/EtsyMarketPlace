namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

public sealed class OrderFulfillmentStudioTests
{
    [Fact]
    public async Task EtsyOrderService_ReturnsSampleOrdersAndFiltersCorrectly()
    {
        var service = new EtsyOrderService();
        var orders = await service.GetOrdersAsync();

        Assert.NotEmpty(orders);
        var ingeOrder = orders.FirstOrDefault(o => o.BuyerName.Contains("Inge Neuer"));
        Assert.NotNull(ingeOrder);
        Assert.Equal("IM3720000224", ingeOrder!.IossNumber);
        Assert.Equal("DE", ingeOrder.CountryCode);

        // Arama filtresi testi
        var filtered = await service.GetOrdersAsync("Germany");
        Assert.Single(filtered);
        Assert.Equal("Inge Neuer", filtered[0].BuyerName);

        // Gönderildi işaretleme testi
        await service.MarkOrderAsShippedAsync(ingeOrder.ReceiptId, "Aras Global", "ARAS-12345", "https://label.com");
        var updated = await service.GetOrdersAsync("Inge Neuer");
        Assert.Equal("Shipped", updated[0].Status);
        Assert.Equal("Aras Global", updated[0].SelectedCarrier);
        Assert.Equal("ARAS-12345", updated[0].TrackingCode);
    }

    [Fact]
    public void DesiAndBillableWeight_CalculatesAccurately()
    {
        // 20 x 15 x 10 / 5000 = 3000 / 5000 = 0.60 Desi
        double desi = ArasGlobalQuoteRequest.ComputeDesi(15.0, 20.0, 10.0);
        Assert.Equal(0.60, desi);

        // Ağırlık 0.40 kg < Desi 0.60 kg => Faturalandırılacak ağırlık 0.60 kg
        double billable = ArasGlobalQuoteRequest.ComputeBillableWeight(0.40, 15.0, 20.0, 10.0);
        Assert.Equal(0.60, billable);

        // Ağırlık 1.5 kg > Desi 0.60 kg => Faturalandırılacak ağırlık 1.5 kg
        double billable2 = ArasGlobalQuoteRequest.ComputeBillableWeight(1.5, 15.0, 20.0, 10.0);
        Assert.Equal(1.5, billable2);
    }

    [Fact]
    public async Task ShipmentCreationManager_ManagesProvidersAndValidatesCreation()
    {
        var mockApiClient = new MockArasGlobalApiClient();
        var arasProvider = new ArasGlobalShipmentCreationProvider(mockApiClient);
        var shipEntegraProvider = new ShipEntegraShipmentCreationProvider();
        var navlungoProvider = new NavlungoShipmentCreationProvider();

        var manager = new ShipmentCreationManager(new IShipmentCreationProvider[]
        {
            arasProvider,
            shipEntegraProvider,
            navlungoProvider
        });

        Assert.Equal(3, manager.GetAvailableProviders().Count);

        // ShipEntegra henüz keşif aşamasında olduğu için hata veya bilgilendirme dönmeli
        var seResult = await manager.CreateShipmentAsync("ShipEntegra", new ShipmentCreationContext());
        Assert.False(seResult.IsSuccess);
        Assert.Contains("henüz geliştirme aşamasındadır", seResult.ErrorMessage);

        // Olmayan taşıyıcı kontrolü
        var invalidResult = await manager.CreateShipmentAsync("UnknownCarrier", new ShipmentCreationContext());
        Assert.False(invalidResult.IsSuccess);
        Assert.Contains("kayıtlı değil", invalidResult.ErrorMessage);
    }

    [Fact]
    public async Task ArasGlobalShipmentCreationProvider_ExecutesFullCreationFlow()
    {
        // Token ayarını test ortamında yazalım
        var testSettings = new ArasGlobalSettings
        {
            BearerToken = "Bearer test_valid_long_token_for_unit_tests_12345678901234567890",
            SavedEmail = "test@example.com"
        };
        ArasGlobalSettingsStore.Save(testSettings);

        var mockApiClient = new MockArasGlobalApiClient();
        var provider = new ArasGlobalShipmentCreationProvider(mockApiClient);

        var order = new EtsyOrderFulfillmentItem
        {
            ReceiptId = 4176634453,
            BuyerName = "Inge Neuer",
            StreetAddress = "Conrad-Scholl-Str 2",
            City = "Koblenz",
            PostalCode = "56068",
            CountryCode = "DE",
            CountryName = "Germany",
            IossNumber = "IM3720000224",
            TotalPrice = 11.00m,
            Currency = "USD",
            Items = new List<EtsyOrderItem>
            {
                new()
                {
                    Title = "3D Print Figure",
                    Quantity = 1,
                    Price = 11.00m,
                    HsCode = "3926400000"
                }
            }
        };

        var context = new ShipmentCreationContext
        {
            Order = order,
            WeightKg = 0.4,
            WidthCm = 15,
            LengthCm = 20,
            HeightCm = 10,
            HsCode = "3926400000",
            SelectedSubCarrier = "widect",
            ServiceType = "Eco Express"
        };

        var result = await provider.CreateShipmentAsync(context);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ShipmentId);
        Assert.StartsWith("ARAS-", result.TrackingNumber);
        Assert.NotNull(result.PriceBreakdown);
        Assert.Equal(13.13m, result.PriceBreakdown!.BasePrice);
        Assert.Equal(15.51m, result.PriceBreakdown.ExchangeTotalPrice);
        Assert.Equal(757.74m, result.PriceBreakdown.TotalPrice);
        Assert.Equal(871.4m, result.PriceBreakdown.TotalPriceWithProvisionRate);
    }

    private sealed class MockArasGlobalApiClient : IArasGlobalApiClient
    {
        public Task<List<ArasGlobalQuoteOffer>> FetchLiveQuotesAsync(ArasGlobalQuoteRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<ArasGlobalQuoteOffer>
            {
                new() { Cargo = "Widect", Price = 13.13m, Currency = "USD", ProviderServiceType = "Eco Express", IsActive = true }
            });
        }

        public Task<decimal> TranslateCurrencyAsync(decimal price, string entryCurrency, string exitCurrency, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(9.87m);
        }

        public Task<List<ArasGtipSearchResult>> SearchGtipCodeAsync(string keyword, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<ArasGtipSearchResult>
            {
                new() { Code = "3926400000", Description = "Plastikten diğer eşya" }
            });
        }

        public Task<ArasAdditionalOptions> GetAdditionalOptionsAsync(string destinationCountry, string provider, decimal orderTotalUsd, string currency, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ArasAdditionalOptions
            {
                DefaultMethod = "DDP",
                CustomsFeeRequired = true,
                CustomsProcessFeeRequired = true,
                AvailableMethods = new List<string> { "DDP" }
            });
        }

        public Task<string> GetAdditionalInformationAsync(string provider, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Tüm paketler 1 kg 0.27 desi üzerinden fiyatlandırılmaktadır.");
        }

        public Task<string> CreateShipmentAsync(ArasCreateShipmentRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("mock-shipment-guid-12345");
        }

        public Task<bool> UpdateShipmentAsync(ArasCreateShipmentRequest request, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<ArasShipmentPriceBreakdown> CalculateShipmentPriceAsync(string shipmentId, string provider, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ArasShipmentPriceBreakdown
            {
                ShipmentPriceId = "mock-price-guid-999",
                BasePrice = 13.13m,
                ExchangeTotalPrice = 15.51m,
                ExchangeCurrency = "USD",
                Currency = "TRY",
                ExchangeRate = 48.855m,
                TotalPrice = 757.74m,
                TotalPriceWithProvisionRate = 871.4m,
                AdditionalServices = new List<ArasAdditionalServiceItem>
                {
                    new() { Name = "Gümrük Bedeli", Price = 1.38m },
                    new() { Name = "Gümrük İşlem Bedeli", Price = 0.75m },
                    new() { Name = "Aras Global Hizmet Bedeli", Price = 0.25m }
                }
            });
        }

        public Task<string> GetShipmentLegalDocumentAsync(string shipmentId, string docType, string rawBearerToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("mock-doc-guid-5582");
        }
    }
}
