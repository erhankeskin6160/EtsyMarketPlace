using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Infrastructure.Orders;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public sealed class OrdersAndShippingEnhancementsTests
{
    [Fact]
    public async Task SqliteOrderAddressRepository_PreservesExistingFieldsOnPartialMetadataSave()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"order_addr_test_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteOrderAddressRepository(tempDb);

            // 1. Initial address save
            var initial = new OrderAddressRecord(
                3900001001L,
                "Jane Doe",
                "jane@example.com",
                "+15125550199",
                "123 Main St",
                "Apt 4B",
                "Austin",
                "TX",
                "78701",
                "US",
                "United States",
                "Manual",
                DateTime.UtcNow)
            {
                IossNumber = "IM27600001"
            };
            await repo.SaveAsync(initial);

            // 2. Partial cost & GTIP save (empty street address should NOT wipe existing street address)
            var metaUpdate = new OrderAddressRecord(
                3900001001L,
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "WebManual",
                DateTime.UtcNow)
            {
                ProductCostUsd = 14.50m,
                ShippingCostUsd = 11.25m,
                WeightKg = 0.45,
                WidthCm = 15.0,
                LengthCm = 20.0,
                HeightCm = 10.0,
                GtipCode = "3926.40.00.00.00",
                SelectedCarrier = "ShipEntegra",
                CarrierServiceName = "ShipEntegra Eco",
                TrackingCode = "SE3900001001TR",
                LabelUrl = "https://panel.shipentegra.com/labels/SE3900001001TR.pdf"
            };
            await repo.SaveAsync(metaUpdate);

            var loaded = await repo.GetByReceiptIdAsync(3900001001L);
            Assert.NotNull(loaded);
            Assert.Equal("Jane Doe", loaded!.BuyerName);
            Assert.Equal("123 Main St", loaded.StreetAddress);
            Assert.Equal("+15125550199", loaded.Phone);
            Assert.Equal("IM27600001", loaded.IossNumber);
            Assert.Equal(14.50m, loaded.ProductCostUsd);
            Assert.Equal(11.25m, loaded.ShippingCostUsd);
            Assert.Equal(0.45, loaded.WeightKg, 3);
            Assert.Equal("3926.40.00.00.00", loaded.GtipCode);
            Assert.Equal("ShipEntegra", loaded.SelectedCarrier);
            Assert.Equal("SE3900001001TR", loaded.TrackingCode);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task EtsyOrderFulfillmentService_HydratesSavedPhoneCostGtipAndTrackingEvenWithoutStreetOverride()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"order_fulfill_test_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteOrderAddressRepository(tempDb);
            await repo.SaveAsync(new OrderAddressRecord(
                3900002002L,
                "",
                "",
                "+491701234567",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "WebManual",
                DateTime.UtcNow)
            {
                IossNumber = "IM5280009999",
                ProductCostUsd = 9.80m,
                ShippingCostUsd = 12.40m,
                WeightKg = 0.60,
                WidthCm = 18.0,
                LengthCm = 22.0,
                HeightCm = 12.0,
                GtipCode = "9503.00.70.00.00",
                SelectedCarrier = "Aras Global",
                CarrierServiceName = "Aras Global Express",
                TrackingCode = "AG3900002002"
            });

            var receipt = new EtsyFulfillmentReceipt(
                3900002002L,
                "Hans Muller",
                "hans@example.de",
                "+49000000",
                "Friedrichstr. 10",
                "",
                "Berlin",
                "BE",
                "10117",
                "DE",
                "Germany",
                "",
                39.90m,
                "EUR",
                DateTimeOffset.UtcNow,
                false,
                Array.Empty<string>(),
                [new EtsyFulfillmentLine(1001L, "3D Printed Figurine", 1, 39.90m)]);

            var fulfillmentService = new EtsyOrderFulfillmentService(new StubLogisticsApiClient(receipt), repo);
            var queue = await fulfillmentService.GetFulfillmentQueueAsync("12345");

            Assert.Single(queue);
            var item = queue[0];
            Assert.Equal("Friedrichstr. 10", item.StreetAddress);
            Assert.Equal("+491701234567", item.Phone);
            Assert.Equal("IM5280009999", item.IossNumber);
            Assert.Equal(9.80m, item.ProductCostUsd);
            Assert.Equal(12.40m, item.ShippingCostUsd);
            Assert.Equal("9503.00.70.00.00", item.GtipCode);
            Assert.Equal("Aras Global", item.SelectedCarrier);
            Assert.Equal("AG3900002002", item.TrackingCode);
            Assert.Equal("shipped", item.Status);
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public async Task ShipmentOrchestratorService_SearchHsCodesAndCarrierQuotes_ReturnsValidResults()
    {
        var stubLogistics = new StubLogisticsApiClient(new EtsyFulfillmentReceipt(
            1L, "Test", "test@example.com", "", "123 St", "", "NY", "NY", "10001", "US", "United States", "", 25m, "USD",
            DateTimeOffset.UtcNow, false, Array.Empty<string>(), Array.Empty<EtsyFulfillmentLine>()));
        var orchestrator = new ShipmentOrchestratorService(new ShipmentCreationManager(), stubLogistics);

        var hsItems = orchestrator.SearchHsCodes("3d", 10);
        Assert.NotEmpty(hsItems);
        Assert.Contains(hsItems, x => x.Code.StartsWith("392640", StringComparison.Ordinal) || x.FormattedCode.StartsWith("3926.40", StringComparison.Ordinal));

        var quotesResult = await orchestrator.GetCarrierQuotesAsync(new CarrierQuotesQuery
        {
            CountryCode = "US",
            PostalCode = "10001",
            WeightKg = 0.5,
            LengthCm = 20,
            WidthCm = 15,
            HeightCm = 10,
            DeclaredValueUsd = new decimalUsdValueOrDefault(45m)
        });

        Assert.NotEmpty(quotesResult.Quotes);
        Assert.True(quotesResult.Quotes.All(q => q.PriceUsd > 0));

        var sessions = await orchestrator.GetCarrierSessionsAsync();
        Assert.Equal(4, sessions.Count);
        Assert.Contains(sessions, s => s.CarrierKey == "aras");
        Assert.Contains(sessions, s => s.CarrierKey == "shipentegra");
        Assert.Contains(sessions, s => s.CarrierKey == "navlungo");
        Assert.Contains(sessions, s => s.CarrierKey == "shiptomore");
    }

    [Fact]
    public void ArasGlobalSettings_RefreshTokenAndValidJwt_EvaluatesCorrectly()
    {
        long futureExp = DateTimeOffset.UtcNow.AddMinutes(45).ToUnixTimeSeconds();
        string payloadJson = $"{{\"sub\":\"123\",\"exp\":{futureExp}}}";
        string payloadB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string validJwt = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{payloadB64}.signature1234567890";

        var settings = new EtsyMarketPlace.Domain.Shipping.ArasGlobalSettings
        {
            BearerToken = $"Bearer {validJwt}",
            RefreshToken = "refresh_token_sample_24h_value_123456"
        };

        Assert.Equal(validJwt, settings.CleanToken);
        Assert.True(settings.IsTokenValid);
        Assert.False(settings.IsTokenExpired());
        Assert.True(settings.HasValidRefreshToken);
        Assert.Equal("refresh_token_sample_24h_value_123456", settings.CleanRefreshToken);
    }

    private sealed class StubLogisticsApiClient(EtsyFulfillmentReceipt receipt) : IEtsyLogisticsApiClient
    {
        public Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken cancellationToken = default)
            => Task.FromResult("{}");

        public Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("{}");

        public Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("{}");

        public Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken cancellationToken = default)
            => Task.FromResult("{}");

        public Task<IReadOnlyList<EtsyFulfillmentReceipt>> GetFulfillmentReceiptsAsync(
            string shopId,
            int lookbackDays = 45,
            bool? wasShipped = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EtsyFulfillmentReceipt>>([receipt]);

        public Task<bool> SubmitTrackingAsync(
            string shopId,
            long receiptId,
            string trackingCode,
            string carrierName,
            bool sendBcc = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
