namespace EtsyMarketPlace.Application.Tests;

using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using Xunit;

public sealed class EtsyLogisticsServiceTests
{
    private sealed class FakeEtsyLogisticsApiClient : IEtsyLogisticsApiClient
    {
        public string ShippingProfilesJson { get; set; } = """
        {
            "count": 2,
            "results": [
                {
                    "shipping_profile_id": 111,
                    "title": "Standart Kargo (3-5 Gun)",
                    "min_processing_days": 3,
                    "max_processing_days": 5
                },
                {
                    "shipping_profile_id": 222,
                    "title": "Hizli Kargo",
                    "min_processing_days": 1,
                    "max_processing_days": 2
                }
            ]
        }
        """;

        public string? ReadinessJson { get; set; } = """
        {
            "count": 1,
            "results": [
                {
                    "readiness_state_id": 999,
                    "readiness_state": "made_to_order",
                    "min_processing_time": 2,
                    "max_processing_time": 4,
                    "processing_time_unit": "business_days"
                }
            ]
        }
        """;

        public string? ActiveListingsJson { get; set; } = "{\"count\": 0, \"results\": []}";

        public string TaxonomyJson { get; set; } = """
        {
            "count": 1,
            "results": [
                {
                    "id": 1042,
                    "name": "Art & Collectibles",
                    "children": [
                        {
                            "id": 1043,
                            "name": "Sculptures",
                            "children": []
                        }
                    ]
                }
            ]
        }
        """;

        public System.Collections.Generic.IReadOnlyList<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt> FulfillmentReceipts { get; set; } = new System.Collections.Generic.List<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt>();
        public bool SubmitTrackingResult { get; set; } = true;

        public Task<System.Collections.Generic.IReadOnlyList<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt>> GetFulfillmentReceiptsAsync(string shopId, int lookbackDays = 45, bool? wasShipped = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(FulfillmentReceipts);

        public Task<bool> SubmitTrackingAsync(string shopId, long receiptId, string trackingCode, string carrierName, bool sendBcc = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(SubmitTrackingResult);

        public Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken ct) =>
            Task.FromResult(ShippingProfilesJson);

        public Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken ct) =>
            Task.FromResult(ReadinessJson);

        public Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken ct) =>
            Task.FromResult(ActiveListingsJson);

        public Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken ct) =>
            Task.FromResult(TaxonomyJson);
    }

    [Fact]
    public async Task GetShippingProfilesAsync_ParsesProfilesCorrectly()
    {
        var fakeClient = new FakeEtsyLogisticsApiClient();
        var service = new EtsyLogisticsService(fakeClient);

        var result = await service.GetShippingProfilesAsync("53236321");

        Assert.Equal(2, result.Count);
        Assert.Equal(111, result[0].ShippingProfileId);
        Assert.Equal("Standart Kargo (3-5 Gun)", result[0].Title);
        Assert.Equal(3, result[0].MinProcessingDays);
        Assert.Equal(5, result[0].MaxProcessingDays);
    }

    [Fact]
    public async Task GetReadinessStatesAsync_ParsesDefinitionsCorrectly()
    {
        var fakeClient = new FakeEtsyLogisticsApiClient();
        var service = new EtsyLogisticsService(fakeClient);

        var result = await service.GetReadinessStatesAsync("53236321");

        Assert.Single(result);
        Assert.Equal(999, result[0].ReadinessStateId);
        Assert.Contains("Siparişe Özel", result[0].Title);
    }

    [Fact]
    public async Task GetSellerTaxonomyNodesAsync_FlattensTreeCorrectly()
    {
        var fakeClient = new FakeEtsyLogisticsApiClient();
        var service = new EtsyLogisticsService(fakeClient);

        var result = await service.GetSellerTaxonomyNodesAsync("53236321");

        Assert.Equal(2, result.Count);
        Assert.Equal("Art & Collectibles", result[0].Path);
        Assert.Equal("Art & Collectibles > Sculptures", result[1].Path);
    }
}
