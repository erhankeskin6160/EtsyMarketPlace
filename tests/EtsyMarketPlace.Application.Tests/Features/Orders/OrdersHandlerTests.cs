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

    private sealed class FakeFulfillmentService : IEtsyOrderFulfillmentService
    {
        public List<EtsyOrderFulfillmentItem> Items { get; set; } = [];

        public Task<IReadOnlyList<EtsyOrderFulfillmentItem>> GetFulfillmentQueueAsync(string shopId, int lookbackDays = 45, bool? wasShipped = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<EtsyOrderFulfillmentItem>>(Items);
        }

        public Task SaveOrderAddressAsync(long receiptId, EtsyOrderFulfillmentItem updatedAddress, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
