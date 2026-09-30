namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Infrastructure.Orders;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class OrderAddressPersistenceAndCsvTests
{
    [Fact]
    public void EtsyOrderCsvParser_ParsesStandardEtsyCsvHeaders()
    {
        string csv = """
        "Order ID","Sale Date","Item Name","Buyer User ID","Full Name","Ship Address1","Ship Address2","Ship City","Ship State","Ship Zipcode","Ship Country"
        "4176634453","2026-09-29","Michael Jackson Statue","ingeneuer","Inge Neuer","123 Maple Street","Suite 200","Orlando","Florida","32801","United States"
        "4174282090","2026-09-28","3D Printed Mask","barbara","Barbara Yapana","456 Oak Avenue","","Los Angeles","CA","90001","US"
        """;

        var records = EtsyOrderCsvParser.ParseCsv(csv);

        Assert.Equal(2, records.Count);

        var r1 = records[0];
        Assert.Equal(4176634453, r1.ReceiptId);
        Assert.Equal("Inge Neuer", r1.BuyerName);
        Assert.Equal("123 Maple Street", r1.StreetAddress);
        Assert.Equal("Suite 200", r1.SecondAddress);
        Assert.Equal("Orlando", r1.City);
        Assert.Equal("FL", r1.State); // Florida -> FL
        Assert.Equal("32801", r1.PostalCode);
        Assert.Equal("US", r1.CountryCode);

        var r2 = records[1];
        Assert.Equal(4174282090, r2.ReceiptId);
        Assert.Equal("Barbara Yapana", r2.BuyerName);
        Assert.Equal("456 Oak Avenue", r2.StreetAddress);
        Assert.Equal("Los Angeles", r2.City);
        Assert.Equal("CA", r2.State);
        Assert.Equal("90001", r2.PostalCode);
        Assert.Equal("US", r2.CountryCode);
    }

    [Fact]
    public void EtsyOrderCsvParser_HandlesCommasInQuotedAddressFields()
    {
        string csv = """
        Order ID,Buyer Name,Street 1,Street 2,City,State,Zip,Country
        4185170234,Anthony Brown,"789 Broadway, 5th Floor, Apt 5A","",New York,NY,10003,USA
        """;

        var records = EtsyOrderCsvParser.ParseCsv(csv);

        Assert.Single(records);
        Assert.Equal(4185170234, records[0].ReceiptId);
        Assert.Equal("Anthony Brown", records[0].BuyerName);
        Assert.Equal("789 Broadway, 5th Floor, Apt 5A", records[0].StreetAddress);
        Assert.Equal("New York", records[0].City);
        Assert.Equal("NY", records[0].State);
        Assert.Equal("10003", records[0].PostalCode);
        Assert.Equal("US", records[0].CountryCode);
    }

    [Fact]
    public async Task SqliteOrderAddressRepository_CrudLifecycle_Succeeds()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"etsy_addr_test_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteOrderAddressRepository(tempDb);
            await repo.InitializeAsync();

            var record1 = new OrderAddressRecord(
                4176634453,
                "Inge Neuer",
                "inge@example.com",
                "+14075551234",
                "123 Maple Street",
                "Suite 200",
                "Orlando",
                "FL",
                "32801",
                "US",
                "United States",
                "EtsyCsv",
                DateTime.UtcNow);

            await repo.SaveAsync(record1);

            var loaded = await repo.GetByReceiptIdAsync(4176634453);
            Assert.NotNull(loaded);
            Assert.Equal("Inge Neuer", loaded.BuyerName);
            Assert.Equal("123 Maple Street", loaded.StreetAddress);
            Assert.Equal("Orlando", loaded.City);
            Assert.Equal("FL", loaded.State);
            Assert.Equal("32801", loaded.PostalCode);
            Assert.Equal("US", loaded.CountryCode);

            // Batch save with update on conflict
            var batch = new List<OrderAddressRecord>
            {
                new(
                    4176634453,
                    "Inge Neuer Updated",
                    "inge@example.com",
                    "",
                    "123 Maple Street Updated",
                    "Apt 5",
                    "Orlando",
                    "FL",
                    "32801",
                    "US",
                    "United States",
                    "Manual",
                    DateTime.UtcNow),
                new(
                    4174282090,
                    "Barbara Yapana",
                    "",
                    "",
                    "456 Oak Ave",
                    "",
                    "Los Angeles",
                    "CA",
                    "90001",
                    "US",
                    "United States",
                    "EtsyCsv",
                    DateTime.UtcNow)
            };

            await repo.SaveBatchAsync(batch);

            var all = await repo.GetAllAsync();
            Assert.Equal(2, all.Count);
            Assert.Equal("123 Maple Street Updated", all[4176634453].StreetAddress);
            Assert.Equal("456 Oak Ave", all[4174282090].StreetAddress);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task EtsyOrderService_HydrateSavedAddresses_PopulatesQueueAndSurvivesSync()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"etsy_order_service_test_{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteOrderAddressRepository(tempDb);
            await repo.InitializeAsync();

            await repo.SaveAsync(new OrderAddressRecord(
                4176634453,
                "Inge Neuer",
                "",
                "",
                "123 Maple St",
                "",
                "Orlando",
                "FL",
                "32801",
                "US",
                "United States",
                "EtsyCsv",
                DateTime.UtcNow));

            var orderService = new EtsyOrderService(repo);

            // API'den gelen sipariş: adres alanları boş (Etsy API redaction)
            var liveOrders = new List<EtsyOrderFulfillmentItem>
            {
                new()
                {
                    ReceiptId = 4176634453,
                    BuyerName = "Inge Neuer",
                    StreetAddress = "", // boş
                    City = "",          // boş
                    PostalCode = "",    // boş
                    CountryCode = "US"
                },
                new()
                {
                    ReceiptId = 4174015409,
                    BuyerName = "Liz Porter",
                    StreetAddress = "",
                    City = "",
                    PostalCode = "",
                    CountryCode = "US"
                }
            };

            orderService.SyncLiveQueue(liveOrders);

            // Öncesinde sokak adresi boş
            var ordersBefore = await orderService.GetOrdersAsync();
            Assert.Equal("", ordersBefore.First(o => o.ReceiptId == 4176634453).StreetAddress);

            // SQLite'tan otomatik hydrate et
            int hydrated = await orderService.HydrateSavedAddressesAsync();
            Assert.Equal(1, hydrated);

            var ordersAfter = await orderService.GetOrdersAsync();
            var o1 = ordersAfter.First(o => o.ReceiptId == 4176634453);
            Assert.Equal("123 Maple St", o1.StreetAddress);
            Assert.Equal("Orlando", o1.City);
            Assert.Equal("FL", o1.State);
            Assert.Equal("32801", o1.PostalCode);

            // Yeniden SyncLiveQueue çağrılsa bile adres silinmemeli
            orderService.SyncLiveQueue(liveOrders);
            var ordersResynced = await orderService.GetOrdersAsync();
            var o1Resynced = ordersResynced.First(o => o.ReceiptId == 4176634453);
            Assert.Equal("123 Maple St", o1Resynced.StreetAddress);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task EtsyOrderService_ApplyAddressRecords_ImmediatelyUpdatesQueue()
    {
        var orderService = new EtsyOrderService();
        var liveOrders = new List<EtsyOrderFulfillmentItem>
        {
            new() { ReceiptId = 1001, BuyerName = "Alice", StreetAddress = "", City = "", CountryCode = "US" },
            new() { ReceiptId = 1002, BuyerName = "Bob", StreetAddress = "", City = "", CountryCode = "US" }
        };

        orderService.SyncLiveQueue(liveOrders);

        var csvRecords = new List<OrderAddressRecord>
        {
            new(1001, "Alice Smith", "", "", "10 Main St", "", "Austin", "TX", "78701", "US", "United States", "EtsyCsv", DateTime.UtcNow),
            new(1002, "Bob Jones", "", "", "20 Elm St", "Apt 2", "Miami", "FL", "33101", "US", "United States", "EtsyCsv", DateTime.UtcNow)
        };

        int matched = orderService.ApplyAddressRecords(csvRecords);
        Assert.Equal(2, matched);

        var current = await orderService.GetOrdersAsync();
        var a1 = current.First(o => o.ReceiptId == 1001);
        Assert.Equal("10 Main St", a1.StreetAddress);
        Assert.Equal("Austin", a1.City);
        Assert.Equal("TX", a1.State);
        Assert.Equal("78701", a1.PostalCode);

        var b1 = current.First(o => o.ReceiptId == 1002);
        Assert.Equal("20 Elm St", b1.StreetAddress);
        Assert.Equal("Miami", b1.City);
        Assert.Equal("FL", b1.State);
        Assert.Equal("33101", b1.PostalCode);
    }
}
