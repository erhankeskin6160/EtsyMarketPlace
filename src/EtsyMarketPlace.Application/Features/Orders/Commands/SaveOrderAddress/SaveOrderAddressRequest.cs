namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveOrderAddress;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Domain.Orders;
using MediatR;

public class SaveOrderAddressRequest : IRequest<ServiceResponse>
{
    public long ReceiptId { get; set; }
    public EtsyOrderFulfillmentItem? UpdatedAddress { get; set; }
}
