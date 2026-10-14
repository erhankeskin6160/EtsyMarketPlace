namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using Xunit;

public sealed class EtsyOrderFulfillmentServiceTests
{
    private sealed class FakeEtsyLogisticsApiClient : IEtsyLogisticsApiClient
    {
        public List<EtsyFulfillmentReceipt> Receipts { get; set; } = new();

        public Task<IReadOnlyList<EtsyFulfillmentReceipt>> GetFulfillmentReceiptsAsync(string shopId, int lookbackDays = 45, bool? wasShipped = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EtsyFulfillmentReceipt>>(Receipts);

        public Task<bool> SubmitTrackingAsync(string shopId, long receiptId, string trackingCode, string carrierName, bool sendBcc = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult("{}");
        public Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("{}");
        public Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("{}");
        public Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult("{}");
    }

    private sealed class FakeOrderAddressRepository : IOrderAddressRepository
    {
        public Dictionary<long, OrderAddressRecord> Store { get; set; } = new();

        public Task<IReadOnlyDictionary<long, OrderAddressRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<long, OrderAddressRecord>>(Store);

        public Task<OrderAddressRecord?> GetByReceiptIdAsync(long receiptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.TryGetValue(receiptId, out var record) ? record : null);

        public Task SaveAsync(OrderAddressRecord record, CancellationToken cancellationToken = default)
        {
            Store[record.ReceiptId] = record;
            return Task.CompletedTask;
        }

        public Task SaveBatchAsync(IEnumerable<OrderAddressRecord> records, CancellationToken cancellationToken = default)
        {
            foreach (var r in records) Store[r.ReceiptId] = r;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(long receiptId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Store.Remove(receiptId));
        }
    }

    [Fact]
    public async Task GetFulfillmentQueueAsync_ReturnsParsedOrdersWithAddresses()
    {
        // Arrange
        var fakeClient = new FakeEtsyLogisticsApiClient
        {
            Receipts = new List<EtsyFulfillmentReceipt>
            {
                new(
                    ReceiptId: 101,
                    BuyerName: "Jane Doe",
                    BuyerEmail: "jane@example.com",
                    Phone: "+15551234567",
                    StreetAddress: "123 Main St",
                    SecondAddress: "Apt 4B",
                    City: "Austin",
                    State: "TX",
                    PostalCode: "78701",
                    CountryCode: "US",
                    CountryName: "United States",
                    IossNumber: "IM1234567890",
                    TotalPrice: 49.99m,
                    Currency: "USD",
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-2),
                    WasShipped: false,
                    TrackingCodes: new List<string>(),
                    Lines: new List<EtsyFulfillmentLine>
                    {
                        new(ListingId: 555, Title: "Handmade Ceramic Vase", Quantity: 1, UnitPrice: 49.99m)
                    })
            }
        };

        var service = new EtsyOrderFulfillmentService(fakeClient);

        // Act
        var result = await service.GetFulfillmentQueueAsync("53236321", 45);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var order = result[0];
        Assert.Equal(101, order.ReceiptId);
        Assert.Equal("Jane Doe", order.BuyerName);
        Assert.Equal("123 Main St", order.StreetAddress);
        Assert.Equal("Apt 4B", order.SecondAddress);
        Assert.Equal("Austin", order.City);
        Assert.Equal("TX", order.State);
        Assert.Equal("78701", order.PostalCode);
        Assert.Equal("US", order.CountryCode);
        Assert.Equal("IM1234567890", order.IossNumber);
        Assert.False(order.IsAddressMissing);
        Assert.Equal("unfulfilled", order.Status);
        Assert.Single(order.Items);
        Assert.Equal("Handmade Ceramic Vase", order.Items[0].Title);
    }

    [Fact]
    public async Task GetFulfillmentQueueAsync_AppliesSavedAddressOverrides()
    {
        // Arrange
        var fakeClient = new FakeEtsyLogisticsApiClient
        {
            Receipts = new List<EtsyFulfillmentReceipt>
            {
                new(
                    ReceiptId: 202,
                    BuyerName: "Old Name",
                    BuyerEmail: "old@example.com",
                    Phone: "",
                    StreetAddress: "", // Missing originally
                    SecondAddress: "",
                    City: "",
                    State: "",
                    PostalCode: "",
                    CountryCode: "US",
                    CountryName: "United States",
                    IossNumber: "",
                    TotalPrice: 35.00m,
                    Currency: "USD",
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
                    WasShipped: false,
                    TrackingCodes: new List<string>(),
                    Lines: new List<EtsyFulfillmentLine>())
            }
        };

        var fakeRepo = new FakeOrderAddressRepository
        {
            Store = new Dictionary<long, OrderAddressRecord>
            {
                [202] = new OrderAddressRecord(
                    202,
                    "Updated Buyer",
                    "updated@example.com",
                    "+19876543210",
                    "789 Broadway",
                    "Suite 100",
                    "New York",
                    "NY",
                    "10003",
                    "US",
                    "United States",
                    "Manual",
                    DateTime.UtcNow)
            }
        };

        var service = new EtsyOrderFulfillmentService(fakeClient, fakeRepo);

        // Act
        var result = await service.GetFulfillmentQueueAsync("53236321", 45);

        // Assert
        Assert.Single(result);
        var order = result[0];
        Assert.Equal("Updated Buyer", order.BuyerName);
        Assert.Equal("789 Broadway", order.StreetAddress);
        Assert.Equal("Suite 100", order.SecondAddress);
        Assert.Equal("New York", order.City);
        Assert.Equal("NY", order.State);
        Assert.Equal("10003", order.PostalCode);
        Assert.False(order.IsAddressMissing);
    }
}
