namespace EtsyMarketPlace.Application.Tests.Features.Orders;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Orders.Commands.SyncExtensionOrders;
using EtsyMarketPlace.Domain.Orders;
using Xunit;

public sealed class SyncExtensionOrdersHandlerTests
{
    private sealed class FakeIntegrationRepository : IEtsyIntegrationRepository
    {
        public List<(string ShopId, string OrderId, string? Notes)> UpsertedCosts { get; } = [];

        public Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);
        public Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyBankPayout>>([]);
        public Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyMonthlyOrderSummary>>([]);
        public Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyListingTrafficRecord>>([]);
        public Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken cancellationToken = default) => Task.FromResult<EtsyChartSnapshot?>(null);
        public Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<long, EtsyDashboardOrderCost>>(new Dictionary<long, EtsyDashboardOrderCost>());

        public Task UpsertOrderCostAsync(
            string shopId, 
            string orderId, 
            decimal? productCost, 
            decimal? shippingCost, 
            decimal? packagingCost, 
            string? notes, 
            CancellationToken cancellationToken = default)
        {
            UpsertedCosts.Add((shopId, orderId, notes));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrderAddressRepository : IOrderAddressRepository
    {
        public Dictionary<long, OrderAddressRecord> Store { get; } = new();

        public Task<OrderAddressRecord?> GetByReceiptIdAsync(long receiptId, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(receiptId, out var rec);
            return Task.FromResult(rec);
        }

        public Task<IReadOnlyDictionary<long, OrderAddressRecord>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<long, OrderAddressRecord>>(Store);
        }

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
    }

    [Fact]
    public async Task Handle_WithGermanyOrder_NormalizesCountryToDE_AndResolvesDefaultIoss()
    {
        var repo = new FakeIntegrationRepository();
        var addressRepo = new FakeOrderAddressRepository();
        var handler = new SyncExtensionOrdersHandler(repo, addressRepo);

        var request = new SyncExtensionOrdersRequest
        {
            ShopId = "53236321",
            Orders =
            [
                new ExtensionOrderSyncItemDto(
                    ReceiptId: "4204665068",
                    BuyerName: "Inge Neuer",
                    ShippingAddress: "Viktoriastr. 12, 56068 Koblenz, Germany",
                    StreetAddress: "Viktoriastr. 12",
                    SecondAddress: "",
                    City: "Koblenz",
                    State: "",
                    PostalCode: "56068",
                    Country: "Germany",
                    Phone: "+49 261 123456",
                    RawAddressText: "Inge Neuer\nViktoriastr. 12\n56068 Koblenz\nGermany\nTel: +49 261 123456",
                    IsFullAddress: true,
                    ProductTitle: "Arcane Jinx Statue",
                    OrderTotal: 107.10m,
                    Currency: "USD",
                    TrackingCode: "878474845271"
                )
            ]
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(addressRepo.Store.ContainsKey(4204665068));
        var record = addressRepo.Store[4204665068];
        Assert.Equal("Inge Neuer", record.BuyerName);
        Assert.Equal("DE", record.CountryCode);
        Assert.Equal("Germany", record.CountryName);
        Assert.Equal("IM3720000224", record.IossNumber); // AB ülkeleri için resmi IOSS
        Assert.Equal("Viktoriastr. 12", record.StreetAddress);
        Assert.Equal("Koblenz", record.City);
        Assert.Equal("56068", record.PostalCode);
        Assert.Equal("878474845271", record.TrackingCode);
    }

    [Fact]
    public async Task Handle_WithUnexpandedSummary_PreservesExistingStreetAddress()
    {
        var repo = new FakeIntegrationRepository();
        var addressRepo = new FakeOrderAddressRepository();
        
        // Önceden tam sokak adresi kaydedilmiş olsun
        addressRepo.Store[4210985421] = new OrderAddressRecord(
            4210985421,
            "Jessica Benson",
            "",
            "(239) 249-2326",
            "158 Deerfield Ave NE",
            "Apt 2",
            "Port Charlotte",
            "FL",
            "33952-8113",
            "US",
            "United States",
            "EtsyExtension",
            DateTime.UtcNow
        );

        var handler = new SyncExtensionOrdersHandler(repo, addressRepo);

        // Sayfa yenilendiğinde gelen kapalı liste özeti
        var request = new SyncExtensionOrdersRequest
        {
            ShopId = "53236321",
            Orders =
            [
                new ExtensionOrderSyncItemDto(
                    ReceiptId: "4210985421",
                    BuyerName: "Jessica Benson",
                    ShippingAddress: "Port Charlotte, FL",
                    StreetAddress: "", // Kapalı özet sokak adresini bilmez
                    SecondAddress: "",
                    City: "Port Charlotte",
                    State: "FL",
                    PostalCode: "",
                    Country: "United States",
                    Phone: "",
                    RawAddressText: "Jessica Benson\nPort Charlotte, FL",
                    IsFullAddress: false, // Kapalı özet
                    ProductTitle: "Arcane Jinx Statue",
                    OrderTotal: 128.40m,
                    Currency: "USD",
                    TrackingCode: ""
                )
            ]
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        var record = addressRepo.Store[4210985421];
        // Tam sokak adresi kapalı özetle ezilmemiş olmalı!
        Assert.Equal("158 Deerfield Ave NE", record.StreetAddress);
        Assert.Equal("Apt 2", record.SecondAddress);
        Assert.Equal("(239) 249-2326", record.Phone);
        Assert.Equal("Port Charlotte", record.City);
        Assert.Equal("FL", record.State);
    }

    [Fact]
    public async Task Handle_WithTrackingNumber_SetsTrackingCode()
    {
        var repo = new FakeIntegrationRepository();
        var addressRepo = new FakeOrderAddressRepository();
        var handler = new SyncExtensionOrdersHandler(repo, addressRepo);

        var request = new SyncExtensionOrdersRequest
        {
            ShopId = "53236321",
            Orders =
            [
                new ExtensionOrderSyncItemDto(
                    ReceiptId: "4203404262",
                    BuyerName: "Damien Rariço",
                    ShippingAddress: "123 Test St, Miami, FL 33101",
                    StreetAddress: "123 Test St",
                    SecondAddress: "",
                    City: "Miami",
                    State: "FL",
                    PostalCode: "33101",
                    Country: "United States",
                    Phone: "",
                    RawAddressText: "",
                    IsFullAddress: true,
                    ProductTitle: "",
                    OrderTotal: 50.0m,
                    Currency: "USD",
                    TrackingCode: null,
                    TrackingNumber: "FEDEX9876543210"
                )
            ]
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        var record = addressRepo.Store[4203404262];
        Assert.Equal("FEDEX9876543210", record.TrackingCode);
    }

    [Fact]
    public async Task Handle_WithUnitedKingdomOrder_AssignsUkVat()
    {
        var repo = new FakeIntegrationRepository();
        var addressRepo = new FakeOrderAddressRepository();
        var handler = new SyncExtensionOrdersHandler(repo, addressRepo);

        var request = new SyncExtensionOrdersRequest
        {
            ShopId = "53236321",
            Orders =
            [
                new ExtensionOrderSyncItemDto(
                    ReceiptId: "4201112223",
                    BuyerName: "John Smith",
                    ShippingAddress: "10 Downing St, London, SW1A 2AA, UK",
                    StreetAddress: "10 Downing St",
                    SecondAddress: "",
                    City: "London",
                    State: "",
                    PostalCode: "SW1A 2AA",
                    Country: "United Kingdom",
                    Phone: "+44 20 7925 0918",
                    RawAddressText: "",
                    IsFullAddress: true,
                    ProductTitle: "",
                    OrderTotal: 45.0m,
                    Currency: "GBP",
                    TrackingCode: ""
                )
            ]
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        var record = addressRepo.Store[4201112223];
        Assert.Equal("GB", record.CountryCode);
        Assert.Equal("United Kingdom", record.CountryName);
        Assert.Equal("370 6004 28", record.IossNumber); // UK resmi VAT
    }

    [Fact]
    public async Task Handle_WithEmptyOrders_ReturnsBadRequest()
    {
        var repo = new FakeIntegrationRepository();
        var addressRepo = new FakeOrderAddressRepository();
        var handler = new SyncExtensionOrdersHandler(repo, addressRepo);

        var request = new SyncExtensionOrdersRequest
        {
            ShopId = "53236321",
            Orders = []
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
    }
}
