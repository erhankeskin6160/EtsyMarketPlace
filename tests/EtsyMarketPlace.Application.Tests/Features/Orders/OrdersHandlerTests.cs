namespace EtsyMarketPlace.Application.Tests.Features.Orders;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Features.Orders.Commands.AiParseAddress;
using EtsyMarketPlace.Application.Features.Orders.Commands.SaveFulfillmentMeta;
using EtsyMarketPlace.Application.Features.Orders.Commands.SaveOrderAddress;
using EtsyMarketPlace.Application.Features.Orders.Queries.GetFulfillmentQueue;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;
using Xunit;

public sealed class OrdersHandlerTests
{
    [Fact]
    public void SaveOrderAddressValidator_RejectsInvalidInput()
    {
        var validator = new SaveOrderAddressValidator();
        var request = new SaveOrderAddressRequest { ReceiptId = 0, UpdatedAddress = null };
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ReceiptId");
        Assert.Contains(result.Errors, e => e.PropertyName == "UpdatedAddress");
    }

    [Fact]
    public void SaveFulfillmentMetaValidator_RejectsInvalidReceiptId()
    {
        var validator = new SaveFulfillmentMetaValidator();
        var request = new SaveFulfillmentMetaRequest { ReceiptId = -1, UpdatedMeta = null };
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ReceiptId");
    }

    [Fact]
    public void AiParseAddressValidator_RejectsEmptyText()
    {
        var validator = new AiParseAddressValidator();
        var request = new AiParseAddressRequest { RawAddress = "" };
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RawAddress");
    }

    [Fact]
    public async Task GetFulfillmentQueueHandler_ReturnsQueueInServiceResponse()
    {
        var fakeFulfillment = new FakeFulfillmentService
        {
            Items =
            [
                new EtsyOrderFulfillmentItem
                {
                    ReceiptId = 12345,
                    BuyerName = "Jane Doe",
                    CountryCode = "US"
                }
            ]
        };

        var handler = new GetFulfillmentQueueHandler(fakeFulfillment);
        var request = new GetFulfillmentQueueRequest { ShopId = "53236321", LookbackDays = 30 };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task GetReceiptDetailHandler_ReturnsReceiptDetail_WhenReceiptExists()
    {
        var fakeFulfillment = new FakeFulfillmentService
        {
            Items =
            [
                new EtsyOrderFulfillmentItem
                {
                    ReceiptId = 4210985421,
                    BuyerName = "Jessica Benson",
                    StreetAddress = "156 Deerfield Ave NE",
                    City = "Port Charlotte",
                    State = "FL",
                    PostalCode = "33952-8113",
                    CountryCode = "US"
                }
            ]
        };

        var handler = new EtsyMarketPlace.Application.Features.Orders.Queries.GetReceiptDetail.GetReceiptDetailHandler(fakeFulfillment);
        var request = new EtsyMarketPlace.Application.Features.Orders.Queries.GetReceiptDetail.GetReceiptDetailRequest
        {
            ShopId = "53236321",
            ReceiptId = 4210985421
        };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        var item = Assert.IsType<EtsyOrderFulfillmentItem>(response.Data);
        Assert.Equal("Jessica Benson", item.BuyerName);
    }

    [Fact]
    public void ApplyRecord_NeverOverwrites_RealBuyerName_WithFallbackPlaceholder()
    {
        var target = new EtsyOrderFulfillmentItem
        {
            ReceiptId = 4210985421,
            BuyerName = "Jessica Benson"
        };

        var recordWithFallback = new OrderAddressRecord(
            4210985421,
            "Etsy Siparişi #4210985421",
            "",
            "",
            "156 Deerfield Ave NE",
            "",
            "Port Charlotte",
            "FL",
            "33952-8113",
            "US",
            "United States",
            "WebManual",
            DateTime.UtcNow);

        EtsyOrderService.ApplyRecord(target, recordWithFallback);

        Assert.Equal("Jessica Benson", target.BuyerName);
        Assert.Equal("156 Deerfield Ave NE", target.StreetAddress);
    }

    private sealed class FakeFulfillmentService : IEtsyOrderFulfillmentService
    {
        public List<EtsyOrderFulfillmentItem> Items { get; set; } = [];

        public Task<IReadOnlyList<EtsyOrderFulfillmentItem>> GetFulfillmentQueueAsync(string shopId, int lookbackDays = 45, bool? wasShipped = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<EtsyOrderFulfillmentItem>>(Items);
        }

        public Task<EtsyOrderFulfillmentItem?> GetReceiptDetailAsync(string shopId, long receiptId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(i => i.ReceiptId == receiptId));
        }

        public Task SaveOrderAddressAsync(long receiptId, EtsyOrderFulfillmentItem updatedAddress, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
